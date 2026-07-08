/*
  Totem 04 — O Ritmo das Escolhas
  TeensyButtonManager.cs

  Responsavel por:
  - Achar e abrir a porta serial do Teensy sozinho (nao importa em qual USB do PC ele for plugado)
  - Ler os eventos "BTN,COR,DOWN" / "BTN,COR,UP" e disparar eventos C#
  - Enviar comandos "LED,COR,ON" / "LED,COR,OFF" / "LED,ALL,OFF" pro Teensy

  Como a autodeteccao funciona:
  - O script varre todas as portas seriais disponiveis no PC
  - Pra cada uma, manda "PING" e espera a resposta "PONG"
  - So considera "achei o Teensy" quando recebe o PONG certo (assim nao confunde
    com mouse/modem/outro dispositivo serial que porventura esteja plugado)
  - Se a conexao cair (desplugou o cabo), ele tenta reconectar sozinho de tempos em tempos

  Como usar:
  1) Arraste este script pra um GameObject vazio na cena (ex: "TeensyManager")
  2) Nao precisa mexer em nada no Inspector — ele acha a porta sozinho
     (o campo "Porta Preferida" e opcional, so use se quiser forcar uma porta especifica)
  3) Em outro script (a logica do jogo), inscreva-se nos eventos:

     void OnEnable() {
         TeensyButtonManager.Instance.OnButtonDown += TratarBotaoPressionado;
     }

     void TratarBotaoPressionado(CorBotao cor) {
         // aqui entra a interacao do jogo:
         // comparar com a sequencia, avancar o jogo, tocar som, etc.
     }

  Requisitos no Unity:
  - Project Settings > Player > Api Compatibility Level: .NET Framework (nao .NET Standard 2.1)
    Isso e necessario pra "System.IO.Ports" funcionar.
*/

using System;
using System.Collections;
using System.IO.Ports;
using UnityEngine;

public enum CorBotao { Azul, Verde, Amarelo, Vermelho }

public class TeensyButtonManager : MonoBehaviour
{
    public static TeensyButtonManager Instance { get; private set; }

    [Header("Conexao automatica (USB)")]
    [Tooltip("Deixe em branco: o script procura o Teensy sozinho em qualquer porta USB. So preencha (ex: COM3) se quiser forcar uma porta especifica.")]
    public string portaPreferida = "";
    public int baudRate = 9600;
    [Tooltip("Quanto tempo (ms) esperar a resposta 'PONG' de cada porta testada")]
    public int timeoutHandshakeMs = 400;
    [Tooltip("Segundos entre tentativas de reconexao quando o Teensy nao esta conectado")]
    public float intervaloReconexao = 3f;

    private SerialPort porta;
    private bool conectado = false;
    private string bufferLeitura = "";

    // Eventos que a logica do jogo vai escutar
    public event Action<CorBotao> OnButtonDown;
    public event Action<CorBotao> OnButtonUp;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        StartCoroutine(ManterConexao());
    }

    // Fica de olho na conexao a vida toda: tenta conectar se estiver desconectado,
    // e da um tempo entre tentativas pra nao travar o jogo nem floodar a varredura de portas
    private IEnumerator ManterConexao()
    {
        while (true)
        {
            if (!conectado)
            {
                TentarConectar();
            }
            yield return new WaitForSeconds(conectado ? 1f : intervaloReconexao);
        }
    }

    private void TentarConectar()
    {
        // Se preencheu uma porta especifica no Inspector, tenta ela primeiro
        if (!string.IsNullOrEmpty(portaPreferida) && TestarPorta(portaPreferida))
            return;

        string[] portas = SerialPort.GetPortNames();
        foreach (string nomePorta in portas)
        {
            if (nomePorta == portaPreferida) continue; // ja tentou essa acima
            if (TestarPorta(nomePorta))
                return;
        }

        Debug.LogWarning("[Teensy] Nenhuma porta USB respondeu como o Teensy do totem. Vai tentar de novo em " + intervaloReconexao + "s.");
    }

    // Abre a porta, manda PING e so aceita como valida se vier PONG de volta.
    // Isso evita conectar em qualquer outro dispositivo serial que esteja no PC.
    private bool TestarPorta(string nomePorta)
    {
        SerialPort tentativa = null;
        try
        {
            tentativa = new SerialPort(nomePorta, baudRate);
            tentativa.ReadTimeout = timeoutHandshakeMs;
            tentativa.WriteTimeout = timeoutHandshakeMs;
            tentativa.NewLine = "\n";
            tentativa.Open();

            // Descarta qualquer lixo que ja tenha chegado (ex: "TOTEM04,READY" do boot)
            tentativa.DiscardInBuffer();

            tentativa.WriteLine("PING");

            string resposta = tentativa.ReadLine(); // pode lancar TimeoutException se ninguem responder
            resposta = resposta.Trim();

            if (resposta == "PONG")
            {
                porta = tentativa;
                conectado = true;
                bufferLeitura = "";
                Debug.Log("[Teensy] Encontrado e conectado na porta " + nomePorta);
                return true;
            }

            tentativa.Close();
            return false;
        }
        catch (Exception)
        {
            // Porta nao existe, esta em uso por outro programa, nao respondeu a tempo, etc.
            // Nao e erro real, so significa "nao e essa porta" — ignora e tenta a proxima.
            if (tentativa != null && tentativa.IsOpen) tentativa.Close();
            return false;
        }
    }

    private void Update()
    {
        if (!conectado || porta == null || !porta.IsOpen) return;

        try
        {
            while (porta.BytesToRead > 0)
            {
                int b = porta.ReadByte();
                if (b == -1) break;

                char c = (char)b;
                if (c == '\n')
                {
                    ProcessarLinha(bufferLeitura);
                    bufferLeitura = "";
                }
                else if (c != '\r')
                {
                    bufferLeitura += c;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Teensy] Conexao perdida (" + e.Message + "). Vai tentar reconectar.");
            FecharConexao();
        }
    }

    private void ProcessarLinha(string linha)
    {
        if (string.IsNullOrEmpty(linha)) return;
        linha = linha.Trim();

        string[] partes = linha.Split(',');
        if (partes.Length != 3) return;

        string tipo = partes[0];
        string corTexto = partes[1];
        string acao = partes[2];

        if (tipo != "BTN") return;

        CorBotao? cor = TextoParaCor(corTexto);
        if (cor == null) return;

        if (acao == "DOWN")
        {
            OnButtonDown?.Invoke(cor.Value);
        }
        else if (acao == "UP")
        {
            OnButtonUp?.Invoke(cor.Value);
        }
    }

    private CorBotao? TextoParaCor(string texto)
    {
        switch (texto)
        {
            case "AZUL": return CorBotao.Azul;
            case "VERDE": return CorBotao.Verde;
            case "AMARELO": return CorBotao.Amarelo;
            case "VERMELHO": return CorBotao.Vermelho;
            default: return null;
        }
    }

    private string CorParaTexto(CorBotao cor)
    {
        switch (cor)
        {
            case CorBotao.Azul: return "AZUL";
            case CorBotao.Verde: return "VERDE";
            case CorBotao.Amarelo: return "AMARELO";
            case CorBotao.Vermelho: return "VERMELHO";
            default: return "";
        }
    }

    /// <summary>
    /// Forca o LED de uma cor especifica a ligar ou desligar.
    /// Util pra "tocar" a sequencia que o jogador precisa repetir.
    /// </summary>
    public void SetLed(CorBotao cor, bool ligado)
    {
        EnviarComando("LED," + CorParaTexto(cor) + "," + (ligado ? "ON" : "OFF"));
    }

    /// <summary>
    /// Apaga todos os LEDs e devolve o controle de feedback local pro Teensy
    /// (ou seja, volta a acender/apagar sozinho quando o botao fisico e apertado).
    /// </summary>
    public void ApagarTodosLeds()
    {
        EnviarComando("LED,ALL,OFF");
    }

    private void EnviarComando(string comando)
    {
        if (!conectado || porta == null || !porta.IsOpen) return;

        try
        {
            porta.WriteLine(comando);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Teensy] Erro enviando comando '" + comando + "': " + e.Message);
        }
    }

    private void OnApplicationQuit()
    {
        FecharConexao();
    }

    private void OnDestroy()
    {
        FecharConexao();
    }

    private void FecharConexao()
    {
        conectado = false;
        if (porta != null)
        {
            try { if (porta.IsOpen) porta.Close(); } catch { }
            porta = null;
        }
    }
}