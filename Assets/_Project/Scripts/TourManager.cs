using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// RESPONSABILIDADE: Orquestrar o fluxo do tour de forma reativa e segura.
/// Modificado para respeitar a barreira de carregamento assíncrono do Meta Quest 2,
/// evitando NullReferenceExceptions e garantindo a transição fluida de estados.
/// </summary>
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(TourDataManager))]
[RequireComponent(typeof(TourUIManager))] 
public class TourManager : MonoBehaviour
{
    [Header("Componentes (Auto-detecta no Awake)")]
    [SerializeField] private TourDataManager dataManager;
    [SerializeField] private TourUIManager uiManager;
    [SerializeField] private AudioSource audioSource;
    
    [Header("Referências da Cena")]
    [Tooltip("O objeto Renderer da esfera que exibirá o panorama 360°.")]
    public Renderer panoramaSphereRenderer;
    
    [Header("Configurações de Cena")]
    public string lobbySceneName = "LobbyScene";
    
    [Header("Configurações de Feedback")]
    [Tooltip("Tempo (em segundos) que o feedback de cor permanece no botão.")]
    public float feedbackDelay = 1.5f;

    [Header("Configurações de Transição (Fades)")]
    [Tooltip("Fade RÁPIDO entre perguntas do mesmo local.")]
    public float questionFadeDuration = 0.5f; 
    
    [Tooltip("Fade LENTO e dramático ao trocar de MAPA.")]
    public float mapTransitionFadeDuration = 2.0f;

    [Tooltip("Tempo extra na tela preta entre mapas para leitura ou amortecimento de I/O.")]
    public float waitOnBlackScreenDelay = 1.5f;

    // --- CONTROLE DE ESTADO INTERNO ---
    private List<DadosLocal> locais;
    private int currentLocationIndex = 0;
    private int currentDesafioIndex = 0;
    private bool isProcessingAnswer = false;

    private void Awake()
    {
        // Garante a auto-atribuição para diminuir erros de Inspector fora da engine
        if (dataManager == null) dataManager = GetComponent<TourDataManager>();
        if (uiManager == null) uiManager = GetComponent<TourUIManager>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        
        audioSource.loop = true;
        audioSource.playOnAwake = false;
    }

    private void Start()
    {
        // Bloqueia interações de botões iniciais de forma segura
        uiManager.SetButtonsInteractable(false);
        
        // Inicia a rotina de inicialização via Barreira Assíncrona
        StartCoroutine(InitializeTourSequence());
    }

    /// <summary>
    /// Barreira Assíncrona: Espera até que o TourDataManager termine o processamento do lote 
    /// de materiais no background thread antes de tentar ler os dados no Runtime.
    /// </summary>
    private IEnumerator InitializeTourSequence()
    {
        // 1. Garante que a tela comece totalmente preta para não quebrar a imersão VR
        yield return StartCoroutine(uiManager.FadeOut(0f));

        // [NOVO] Informa proativamente o usuário VR que os assets tridimensionais estão sendo carregados
        uiManager.ShowTransitionText("Carregando Experiência VR...\nPor favor, aguarde.");

        // 2. Aguarda o sinalizador de conclusão do carregamento assíncrono de assets
        while (!dataManager.IsDataLoaded)
        {
            yield return null; 
        }

        locais = dataManager.Locais;

        // Verifica integridade dos dados injetados
        if (locais == null || locais.Count == 0)
        {
            Debug.LogError("[TourManager] Erro Crítico: Nenhum dado de local foi carregado pelo TourDataManager.");
            yield break;
        }

        if (GameSettings.Instance != null)
        {
            currentLocationIndex = GameSettings.Instance.selectedLocationIndex;
            if (currentLocationIndex < 0 || currentLocationIndex >= locais.Count)
            {
                currentLocationIndex = 0;
            }
        }
        else
        {
            currentLocationIndex = 0;
        }

        currentDesafioIndex = 0;

        // [NOVO] Altera temporariamente a mensagem para indicar que o mapa específico está abrindo antes do FadeIn
        uiManager.ShowTransitionText($"Entrando em:\n{locais[currentLocationIndex].locationName}");
        
        CarregarDadosDoLocal(currentLocationIndex);
        AtualizarDesafioAtual();

        // Aguarda um breve instante para o usuário ler o nome do mapa de destino
        yield return new WaitForSeconds(waitOnBlackScreenDelay);
        
        // Remove a mensagem de carregamento antes de revelar a imagem 360
        uiManager.HideTransitionText();

        yield return StartCoroutine(uiManager.FadeIn(mapTransitionFadeDuration));
        uiManager.SetButtonsInteractable(true);
    }

    private void CarregarDadosDoLocal(int localIndex)
    {
        if (localIndex < 0 || localIndex >= locais.Count) return;

        DadosLocal localAtual = locais[localIndex];

        // Aplica o clipe de áudio em cache com segurança de concorrência
        if (localAtual.backgroundMusic != null)
        {
            audioSource.clip = localAtual.backgroundMusic;
            audioSource.Play();
        }
        else
        {
            audioSource.Stop();
        }
    }

    private void AtualizarDesafioAtual()
    {
        DadosLocal localAtual = locais[currentLocationIndex];
        
        if (localAtual.desafios == null || localAtual.desafios.Count == 0)
        {
            Debug.LogError($"[TourManager] O local '{localAtual.locationName}' não possui desafios configurados.");
            return;
        }

        Desafio desafioAtual = localAtual.desafios[currentDesafioIndex];

        // Atualiza a projeção esférica 360 no Skybox/Sphere Material
        if (desafioAtual.panoramaMaterial != null)
        {
            panoramaSphereRenderer.material = desafioAtual.panoramaMaterial;
        }
        else
        {
            Debug.LogWarning($"[TourManager] Desafio {currentDesafioIndex} sem material de panorama mapeado.");
        }

        // Aplica a rotação de compensação para calibração de visão inicial do Quest 2
        panoramaSphereRenderer.transform.rotation = Quaternion.Euler(0, desafioAtual.initialYRotation, 0);

        // Atualiza os dados puramente visuais na interface do canvas XR
        uiManager.SetupQuiz(desafioAtual.questionText, desafioAtual.answers, this);
    }

    /// <summary>
    /// Ponto de entrada disparado de forma reativa pelo clique dos botões do TourUIManager.
    /// </summary>
    public void OnAnswerSelected(int indexSelecionado)
    {
        if (isProcessingAnswer) return;
        StartCoroutine(ProcessAnswerSequence(indexSelecionado));
    }

    private IEnumerator ProcessAnswerSequence(int indexSelecionado)
    {
        isProcessingAnswer = true;
        uiManager.SetButtonsInteractable(false);

        Desafio desafioAtual = locais[currentLocationIndex].desafios[currentDesafioIndex];
        bool acertou = (indexSelecionado == desafioAtual.correctAnswerIndex);

        // Aciona o feedback visual de cor no canvas
        uiManager.ApplyButtonFeedback(indexSelecionado, acertou);

        yield return new WaitForSeconds(feedbackDelay);

        uiManager.ResetButtonColors();

        if (acertou)
        {
            // Avança para o próximo estado do Quiz
            currentDesafioIndex++;

            if (currentDesafioIndex < locais[currentLocationIndex].desafios.Count)
            {
                // Próxima pergunta dentro do MESMO mapa (Fade Rápido)
                yield return StartCoroutine(TransitionToNextQuestion());
            }
            else
            {
                // Concluiu todos os desafios deste mapa, avança para o próximo Local (Fade Lento)
                int proximoLocalIndex = currentLocationIndex + 1;

                if (proximoLocalIndex < locais.Count)
                {
                    currentLocationIndex = proximoLocalIndex;
                    currentDesafioIndex = 0;
                    yield return StartCoroutine(TransitionToNextMap(currentLocationIndex));
                }
                else
                {
                    // Fim total da jornada do Tour, retorna ao menu principal
                    yield return StartCoroutine(ReturnToLobby());
                }
            }
        }
        
        uiManager.SetButtonsInteractable(true);
        isProcessingAnswer = false;
    }

    private IEnumerator TransitionToNextQuestion()
    {
        yield return StartCoroutine(uiManager.FadeOut(questionFadeDuration));
        AtualizarDesafioAtual();
        yield return StartCoroutine(uiManager.FadeIn(questionFadeDuration));
    }

    private IEnumerator TransitionToNextMap(int nextMapIndex)
    {
        yield return StartCoroutine(uiManager.FadeOut(mapTransitionFadeDuration));

        string nomeProximo = locais[nextMapIndex].locationName;
        uiManager.ShowTransitionText(nomeProximo);

        yield return new WaitForSeconds(waitOnBlackScreenDelay);

        uiManager.HideTransitionText(); 
        
        CarregarDadosDoLocal(nextMapIndex);
        AtualizarDesafioAtual();

        yield return StartCoroutine(uiManager.FadeIn(mapTransitionFadeDuration));
    }
    
    public void RequestExitToLobby()
    {
        if (isProcessingAnswer) return;
        StartCoroutine(ReturnToLobby());
    }

    private IEnumerator ReturnToLobby()
    {
        uiManager.SetButtonsInteractable(false);
        
        // 1. Escurece a tela suavemente para manter o conforto visual em VR
        yield return StartCoroutine(uiManager.FadeOut(mapTransitionFadeDuration));
        
        // 2. Para a música de fundo imediatamente
        audioSource.Stop();
        
        // [CORREÇÃO CRÍTICA]: Informa o usuário proativamente ANTES da engine travar a thread carregando a cena
        uiManager.ShowTransitionText("Retornando ao Menu Principal...\nPor favor, aguarde.");

        // Corta qualquer frames residuais dando um respiro curto para a UI renderizar o texto acima
        yield return new WaitForSeconds(0.1f);

        // 3. Aciona a limpeza proativa e imediata de memória RAM/VRAM que criamos no DataManager
        if (dataManager != null)
        {
            dataManager.LimparAssetsCarregados();
        }

        // 4. Carrega a cena do Lobby de forma síncrona/direta (protegida pelo texto de transição)
        if (!string.IsNullOrEmpty(lobbySceneName))
        {
            SceneManager.LoadScene(lobbySceneName);
        }
        else
        {
            Debug.LogError("[TourManager] Nome da cena do Lobby inválido!");
        }
    }
}