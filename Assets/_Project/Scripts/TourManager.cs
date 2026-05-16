using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.XR.Interaction.Toolkit; 

/// <summary>
/// RESPONSABILIDADE: Orquestrar o fluxo do tour.
/// Gerencia o estado (local/desafio atual), a lógica do quiz (CheckAnswer),
/// o áudio e as transições de cena.
/// </summary>
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(TourDataManager))]
[RequireComponent(typeof(TourUIManager))] 
public class TourManager : MonoBehaviour
{
    [Header("Componentes (Arraste ou Auto-detecta)")]
    [SerializeField] private TourDataManager dataManager;
    [SerializeField] private TourUIManager uiManager;
    
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

    [Tooltip("Tempo extra na tela preta entre mapas (para ler o texto 'Próxima Parada').")]
    public float waitOnBlackScreenDelay = 3.0f;

    [Header("Áudio")]
    [Tooltip("Som ao completar um local.")]
    public AudioClip locationVictorySound;
    [Range(0f, 1f)] public float backgroundMusicVolume = 0.5f;
    [Range(0f, 1f)] public float sfxVolume = 1.0f;
    public AudioClip correctAnswerSound;
    public AudioClip incorrectAnswerSound;
    
    // --- Variáveis Privadas ---
    private List<DadosLocal> locais = new List<DadosLocal>();
    private int currentLocalIndex = 0;
    private int currentDesafioIndex = 0;
    private bool isAnswering = false; // Trava de cliques
    private AudioSource audioSource;
    private Desafio desafioAtual; 

    void Start()
    {
        audioSource = GetComponent<AudioSource>();

        // Auto-detecta componentes se esquecer de arrastar
        if (dataManager == null) dataManager = GetComponent<TourDataManager>();
        if (uiManager == null) uiManager = GetComponent<TourUIManager>();

        // Inscreve nos eventos da UI
        uiManager.OnAnswerButtonClicked += CheckAnswer;
        uiManager.OnMenuButtonClicked += HandleMenuButtonClick;

        DisablePlayerMovement();
        
        // Verifica se veio do Lobby via Singleton
        GameSettings settings = GameSettings.Instance;
        if (settings != null)
        {
            currentLocalIndex = settings.selectedLocationIndex;
            Destroy(settings.gameObject);
        }
        else
        {
            currentLocalIndex = 0;
        }

        StartCoroutine(InitializeTour());
    }

    void DisablePlayerMovement()
    {
        var moveProvider = FindObjectOfType<UnityEngine.XR.Interaction.Toolkit.ContinuousMoveProviderBase>();
        if (moveProvider != null) moveProvider.enabled = false;

        var teleportProvider = FindObjectOfType<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationProvider>();
        if (teleportProvider != null) teleportProvider.enabled = false;
    }

    // --- INICIALIZAÇÃO ---

    private IEnumerator InitializeTour()
    {
        // 1. Espera carregar o JSON e Assets
        yield return StartCoroutine(dataManager.LoadTourDataFromJSONAsync());
        locais = dataManager.Locais;
        
        // 2. Inicia o primeiro tour
        if (locais.Count > 0)
        {
            // Carrega os dados sem fade out (já estamos carregando)
            CarregarDadosDoLocal(currentLocalIndex);
            // Faz apenas o Fade In inicial (lento para ser suave)
            yield return StartCoroutine(uiManager.FadeIn(mapTransitionFadeDuration));
        }
        else
        {
            Debug.LogError("Nenhum local carregado. Verifique o JSON.");
        }
    }

    // --- LÓGICA PRINCIPAL ---

    void CarregarDadosDoLocal(int localIndex)
    {
        if(locais.Count == 0 || localIndex >= locais.Count) return;
        
        currentLocalIndex = localIndex;
        currentDesafioIndex = 0; 

        // Gerencia Música
        if (locais[currentLocalIndex].backgroundMusic != null)
        {
            audioSource.clip = locais[currentLocalIndex].backgroundMusic;
            audioSource.volume = backgroundMusicVolume;
            audioSource.loop = true;
            audioSource.Play();
        } else {
            audioSource.Stop();
        }
        
        ApresentarDesafioAtual();
    }

    void ApresentarDesafioAtual()
    {
        // Validação de segurança
        if (currentLocalIndex >= locais.Count || currentDesafioIndex >= locais[currentLocalIndex].desafios.Count) return;
        
        desafioAtual = locais[currentLocalIndex].desafios[currentDesafioIndex];
        
        // Atualiza visual (Esfera 360)
        panoramaSphereRenderer.transform.rotation = Quaternion.Euler(0, desafioAtual.initialYRotation, 0);
        panoramaSphereRenderer.material = desafioAtual.panoramaMaterial;
        
        // Atualiza UI (Texto e Botões)
        uiManager.ApresentarDesafio(desafioAtual);
        
        // Destrava interações
        isAnswering = false;
    }

    // --- EVENTOS DA UI ---

    public void CheckAnswer(int selectedIndex)
    {
        if (isAnswering) return; 
        isAnswering = true; 
        uiManager.SetAllButtonsInteractable(false, desafioAtual.answers.Count);

        if (selectedIndex == desafioAtual.correctAnswerIndex)
        {
            StartCoroutine(HandleCorrectAnswer(selectedIndex));
        }
        else
        {
            StartCoroutine(HandleIncorrectAnswer(selectedIndex));
        }
    }

    public void HandleMenuButtonClick()
    {
        if (isAnswering) return; 
        isAnswering = true; 
        
        int answerCount = (desafioAtual != null) ? desafioAtual.answers.Count : uiManager.answerButtons.Count;
        uiManager.SetAllButtonsInteractable(false, answerCount);
        
        StartCoroutine(ReturnToLobby());
    }
    
    // --- CORROTINAS DE FLUXO DO JOGO ---

    private IEnumerator HandleCorrectAnswer(int correctButtonIndex)
    {
        // 1. Feedback Positivo Visual e Sonoro
        uiManager.SetButtonFeedback(correctButtonIndex, uiManager.correctColor);
        if(correctAnswerSound != null) audioSource.PlayOneShot(correctAnswerSound, sfxVolume); 
        
        yield return new WaitForSeconds(feedbackDelay);

        // 2. Avança o índice
        currentDesafioIndex++;
        
        // 3. Decide o próximo passo
        if (currentDesafioIndex >= locais[currentLocalIndex].desafios.Count)
        {
            // ACABOU O LOCAL ATUAL -> Toca som de vitória
            audioSource.Stop(); 
            if (locationVictorySound != null) 
                audioSource.PlayOneShot(locationVictorySound, sfxVolume);

            int proximoLocalIndex = currentLocalIndex + 1;
            
            if (proximoLocalIndex < locais.Count)
            {
                // Ainda tem mapa: Transição Lenta com Texto
                yield return StartCoroutine(TransitionToNextMap(proximoLocalIndex));
            }
            else
            {
                // Acabou tudo: Volta pro Lobby
                yield return StartCoroutine(ReturnToLobby());
            }
        }
        else
        {
            // CONTINUA NO MESMO LOCAL -> Transição Rápida
            yield return StartCoroutine(TransitionToNextQuestion());
        }
    }

    private IEnumerator HandleIncorrectAnswer(int incorrectButtonIndex)
    {
        uiManager.SetButtonFeedback(incorrectButtonIndex, uiManager.incorrectColor);
        if(incorrectAnswerSound != null) audioSource.PlayOneShot(incorrectAnswerSound, sfxVolume);
        
        yield return new WaitForSeconds(feedbackDelay);
        
        // Retry com fade rápido
        yield return StartCoroutine(uiManager.FadeOut(questionFadeDuration));
        uiManager.ResetButtonsToNormal(desafioAtual.answers.Count);
        yield return StartCoroutine(uiManager.FadeIn(questionFadeDuration));

        isAnswering = false;
    }

    // --- CORROTINAS DE TRANSIÇÃO ESPECÍFICAS ---

    /// <summary>
    /// Transição RÁPIDA: Apenas escurece, troca o material/pergunta e clareia.
    /// </summary>
    private IEnumerator TransitionToNextQuestion()
    {
        // Fade Out Rápido
        yield return StartCoroutine(uiManager.FadeOut(questionFadeDuration));
        
        // Troca conteúdo
        ApresentarDesafioAtual();
        
        // Fade In Rápido
        yield return StartCoroutine(uiManager.FadeIn(questionFadeDuration));
    }

    /// <summary>
    /// Transição LENTA: Fade Out -> Mostra Texto -> Espera -> Carrega -> Esconde Texto -> Fade In.
    /// </summary>
    private IEnumerator TransitionToNextMap(int nextMapIndex)
    {
        // 1. Fade Out Lento (Escurece a tela PRIMEIRO)
        // O texto ainda está escondido aqui.
        yield return StartCoroutine(uiManager.FadeOut(mapTransitionFadeDuration));

        // 2. Agora que está tudo preto, mostramos o texto explicativo
        string nomeProximo = locais[nextMapIndex].locationName;
        uiManager.ShowTransitionText(nomeProximo);

        // 3. Espera na tela preta (lendo a mensagem)
        yield return new WaitForSeconds(waitOnBlackScreenDelay);

        // 4. Esconde o texto antes de começar a clarear
        uiManager.HideTransitionText(); 
        
        // 5. Carrega os dados (Textura, Música, etc)
        CarregarDadosDoLocal(nextMapIndex);

        // 6. Fade In Lento (Clareia a tela revelando o novo local)
        yield return StartCoroutine(uiManager.FadeIn(mapTransitionFadeDuration));
    }
    
    private IEnumerator ReturnToLobby()
    {
        yield return StartCoroutine(uiManager.FadeOut(mapTransitionFadeDuration));
        audioSource.Stop();
        
        if (uiManager.HasFadeScreen)
            yield return new WaitForSeconds(1.0f);

        if (!string.IsNullOrEmpty(lobbySceneName))
            SceneManager.LoadScene(lobbySceneName);
    }
}