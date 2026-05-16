using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// RESPONSABILIDADE: Controlar todos os elementos visuais (UI) da cena do Tour.
/// (Textos, Botões, Cores de Feedback, Tela de Fade, Texto de Transição).
/// Não sabe a lógica do jogo, apenas exibe o que o TourManager manda.
/// </summary>
public class TourUIManager : MonoBehaviour
{
    [Header("Referências da UI - Quiz")]
    [Tooltip("O componente de texto para exibir a pergunta do quiz.")]
    public TextMeshProUGUI questionTextUI;
    [Tooltip("A lista de botões que servirão como opções de resposta.")]
    public List<Button> answerButtons;
    
    [Header("Referências da UI - Navegação")]
    [Tooltip("Botão para retornar ao Lobby (Menu).")]
    public Button menuButton;

    [Header("Referências da UI - Transição")]
    [Tooltip("Uma imagem preta (Image UI) para o fade.")]
    public Image fadeScreen;
    [Tooltip("Texto que aparece SOBRE a tela preta indicando o próximo local.")]
    public TextMeshProUGUI transitionTextUI; 

    [Header("Configurações de Feedback Visual")]
    [Tooltip("Cor que o botão de resposta assume ao acertar.")]
    public Color correctColor = new Color(0.1f, 0.7f, 0.2f);
    [Tooltip("Cor que o botão de resposta assume ao errar.")]
    public Color incorrectColor = new Color(0.8f, 0.2f, 0.1f);
    [Tooltip("Cor padrão dos botões de resposta.")]
    public Color normalColor = Color.white;

    // Eventos
    public event System.Action<int> OnAnswerButtonClicked;
    public event System.Action OnMenuButtonClicked;

    // Propriedade pública para o TourManager saber se pode pausar no fade
    public bool HasFadeScreen => fadeScreen != null;

    void Start()
    {
        // Configuração Inicial do Fade Screen
        if (fadeScreen == null)
        {
            Debug.LogWarning("A 'Fade Screen' (Image) não foi atribuída no TourUIManager.");
        }
        else
        {
            // Garante que a tela comece PRETA e ATIVA
            Color tempColor = fadeScreen.color;
            tempColor.a = 1.0f; 
            fadeScreen.color = tempColor;
            fadeScreen.gameObject.SetActive(true);
        }

        // Garante que o texto de transição comece invisível
        if (transitionTextUI != null)
        {
            transitionTextUI.gameObject.SetActive(false);
            transitionTextUI.text = "";
        }

        InitializeButtonListeners();
    }

    private void InitializeButtonListeners()
    {
        for (int i = 0; i < answerButtons.Count; i++)
        {
            int index = i;
            if (answerButtons[i] != null)
            {
                answerButtons[i].onClick.AddListener(() => OnAnswerButtonClicked?.Invoke(index));
            }
        }
        
        if (menuButton != null)
        {
            menuButton.onClick.AddListener(() => OnMenuButtonClicked?.Invoke());
        }
    }

    public void ApresentarDesafio(Desafio desafio)
    {
        questionTextUI.text = desafio.questionText;

        for (int i = 0; i < answerButtons.Count; i++)
        {
            if (i < desafio.answers.Count)
            {
                answerButtons[i].gameObject.SetActive(true);
                answerButtons[i].GetComponent<Image>().color = normalColor;
                answerButtons[i].interactable = true;

                var tmproText = answerButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                if (tmproText != null)
                    tmproText.text = desafio.answers[i];
            }
            else
            {
                answerButtons[i].gameObject.SetActive(false);
            }
        }
        
        if (menuButton != null)
        {
            menuButton.gameObject.SetActive(true);
            menuButton.interactable = true;
        }
    }

    public void SetAllButtonsInteractable(bool isInteractable, int answerCount)
    {
        for(int i = 0; i < answerCount; i++)
        {
            if (i < answerButtons.Count && answerButtons[i] != null) 
                answerButtons[i].interactable = isInteractable;
        }
        
        if (menuButton != null)
            menuButton.interactable = isInteractable;
    }

    public void SetButtonFeedback(int buttonIndex, Color color)
    {
        if (buttonIndex < 0 || buttonIndex >= answerButtons.Count) return;
        answerButtons[buttonIndex].GetComponent<Image>().color = color;
    }

    public void ResetButtonsToNormal(int answerCount)
    {
        for (int i = 0; i < answerCount; i++)
        {
             if(i < answerButtons.Count && answerButtons[i].gameObject.activeInHierarchy) {
                answerButtons[i].GetComponent<Image>().color = normalColor;
                answerButtons[i].interactable = true;
             }
        }
        
        if (menuButton != null)
            menuButton.interactable = true;
    }

    // --- MÉTODOS DE TEXTO DE TRANSIÇÃO ---

    /// <summary>
    /// Exibe o texto indicando o próximo local. 
    /// </summary>
    public void ShowTransitionText(string nextLocationName)
    {
        if (transitionTextUI != null)
        {
            // AQUI ESTÁ A MUDANÇA DA FRASE
            transitionTextUI.text = $"Você agora está sendo transportado para:\n\n<size=140%><color=#FFD700>{nextLocationName}</color></size>";
            transitionTextUI.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// Esconde o texto de transição.
    /// Deve ser chamado antes do Fade In começar.
    /// </summary>
    public void HideTransitionText()
    {
        if (transitionTextUI != null)
        {
            transitionTextUI.gameObject.SetActive(false);
        }
    }

    // --- CORROTINAS DE FADE ---

    public IEnumerator FadeOut(float fadeDuration)
    {
        if (fadeScreen == null) yield break;

        Color currentColor = Color.black; // Baseado em preto
        float startAlpha = fadeScreen.gameObject.activeInHierarchy ? fadeScreen.color.a : 0f;
        float targetAlpha = 1f;
        float timer = 0f;

        fadeScreen.gameObject.SetActive(true);

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / fadeDuration);
            currentColor.a = Mathf.Lerp(startAlpha, targetAlpha, progress);
            fadeScreen.color = currentColor;
            yield return null;
        }
        currentColor.a = targetAlpha;
        fadeScreen.color = currentColor;
    }

    public IEnumerator FadeIn(float fadeDuration)
    {
        if (fadeScreen == null) yield break;

        Color currentColor = Color.black;
        float startAlpha = 1f;
        float targetAlpha = 0f;
        float timer = 0f;

        fadeScreen.gameObject.SetActive(true);

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / fadeDuration);
            currentColor.a = Mathf.Lerp(startAlpha, targetAlpha, progress);
            fadeScreen.color = currentColor;
            yield return null;
        }
        currentColor.a = targetAlpha;
        fadeScreen.color = currentColor;
        fadeScreen.gameObject.SetActive(false);
    }
}