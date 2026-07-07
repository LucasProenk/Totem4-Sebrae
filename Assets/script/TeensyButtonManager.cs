/*
  Totem 04 — O Ritmo das Escolhas
  TeensyButtonManager.cs

  Responsavel por:
  - Abrir a porta serial com o Teensy
  - Ler os eventos "BTN,COR,DOWN" / "BTN,COR,UP" e disparar eventos C#
  - Enviar comandos "LED,COR,ON" / "LED,COR,OFF" / "LED,ALL,OFF" pro Teensy

  Como usar:
  1) Arraste este script pra um GameObject vazio na cena (ex: "TeensyManager")
  2) Ajuste o campo "Porta Serial" no Inspector (ex: COM3 no Windows)
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
using System.IO.Ports;
using UnityEngine;

public enum CorBotao { Azul, Verde, Amarelo, Vermelho }

public class TeensyButtonManager : MonoBehaviour
{
    public static TeensyButtonManager Instance { get; private set; }

    [Header("Configuracao da Serial")]
    [Tooltip("Porta serial do Teensy. No Windows costuma ser algo como COM3, COM4...")]
    public string portaSerial = "COM3";
    public int baudRate = 9600;

    private SerialPort porta;
    private bool conectado = false;

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
        AbrirConexao();
    }

    private void AbrirConexao()
    {
        try
        {
            porta = new SerialPort(portaSerial, baudRate);
            porta.ReadTimeout = 50;
            porta.NewLine = "\n";
            porta.Open();
            conectado = true;
            Debug.Log("[Teensy] Conectado na porta " + portaSerial);
        }
        catch (Exception e)
        {
            conectado = false;
            Debug.LogWarning("[Teensy] Falha ao abrir porta " + portaSerial + ": " + e.Message);
        }
    }

    private void Update()
    {
        if (!conectado || porta == null || !porta.IsOpen) return;

        try
        {
            while (porta.BytesToRead > 0)
            {
                string linha = porta.ReadLine();
                ProcessarLinha(linha);
            }
        }
        catch (TimeoutException)
        {
            // normal quando nao ha dados novos, pode ignorar
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Teensy] Erro lendo serial: " + e.Message);
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
    /// (ou seja, volta a acender sozinho quando o botao fisico e apertado).
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
        if (porta != null && porta.IsOpen)
        {
            porta.Close();
        }
    }
}