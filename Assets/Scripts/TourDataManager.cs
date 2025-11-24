using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// --- ESTRUTURAS DE DADOS PARA O RUNTIME (O QUE O JOGO USA) ---
// (Movidas para cá, pois são a "saída" deste script)

public class Desafio
{
    public Material panoramaMaterial;
    public float initialYRotation;
    public string questionText;
    public List<string> answers;
    public int correctAnswerIndex;
}

public class DadosLocal
{
    public string locationName;
    public AudioClip backgroundMusic;
    public List<Desafio> desafios;
}

// --- ESTRUTURAS DE DADOS PARA O JSON (O QUE O ARQUIVO CONTÉM) ---

[System.Serializable]
public class DesafioJson
{
    public string panoramaMaterialPath;
    public float initialYRotation;
    public string questionText;
    public List<string> answers;
    public int correctAnswerIndex;
}

[System.Serializable]
public class DadosLocalJson
{
    public string locationName;
    public string backgroundMusicPath;
    public string mapImagePath;
    public List<DesafioJson> desafios;
}

[System.Serializable]
public class TourDataJson
{
    public List<DadosLocalJson> locais;
}


/// <summary>
/// RESPONSABILIDADE: Carregar todos os dados do JSON e assets (Materiais, Áudios)
/// da pasta Resources de forma assíncrona.
/// </summary>
public class TourDataManager : MonoBehaviour
{
    [Header("Arquivo de Conteúdo")]
    [Tooltip("Arraste aqui o arquivo JSON que contém os dados dos tours.")]
    public TextAsset tourDataJson;

    // Propriedade pública para o TourManager acessar os dados carregados
    public List<DadosLocal> Locais { get; private set; } = new List<DadosLocal>();

    /// <summary>
    /// Corrotina principal que carrega todos os dados assincronamente.
    /// O TourManager irá esperar por esta corrotina.
    /// </summary>
    public IEnumerator LoadTourDataFromJSONAsync()
    {
        if (tourDataJson == null)
        {
            Debug.LogError("ERRO CRÍTICO: O arquivo 'tourDataJson' não foi atribuído no TourDataManager!");
            yield break; // Para a corrotina
        }

        TourDataJson dataFromJson = JsonUtility.FromJson<TourDataJson>(tourDataJson.text);
        Locais.Clear();

        foreach (var localJson in dataFromJson.locais)
        {
            DadosLocal novoLocal = new DadosLocal
            {
                locationName = localJson.locationName,
                desafios = new List<Desafio>()
            };

            // --- Carregamento Assíncrono do Áudio ---
            if (!string.IsNullOrEmpty(localJson.backgroundMusicPath))
            {
                ResourceRequest audioRequest = Resources.LoadAsync<AudioClip>(localJson.backgroundMusicPath);
                yield return audioRequest; // Espera o carregamento terminar

                if (audioRequest.asset != null)
                {
                    novoLocal.backgroundMusic = audioRequest.asset as AudioClip;
                }
                else
                {
                    Debug.LogWarning($"Asset de Áudio não encontrado em 'Resources/{localJson.backgroundMusicPath}' para o local '{novoLocal.locationName}'");
                }
            }

            // --- Carregamento Assíncrono dos Desafios ---
            foreach(var desafioJson in localJson.desafios)
            {
                Desafio novoDesafio = new Desafio
                {
                    initialYRotation = desafioJson.initialYRotation,
                    questionText = desafioJson.questionText,
                    answers = desafioJson.answers,
                    correctAnswerIndex = desafioJson.correctAnswerIndex
                };

                // --- Carregamento Assíncrono do Material ---
                if (!string.IsNullOrEmpty(desafioJson.panoramaMaterialPath))
                {
                    ResourceRequest materialRequest = Resources.LoadAsync<Material>(desafioJson.panoramaMaterialPath);
                    yield return materialRequest; // Espera o carregamento terminar

                    if (materialRequest.asset != null)
                    {
                        novoDesafio.panoramaMaterial = materialRequest.asset as Material;
                    }
                    else
                    {
                        Debug.LogWarning($"Asset de Material não encontrado em 'Resources/{desafioJson.panoramaMaterialPath}' para o local '{novoLocal.locationName}'");
                    }
                }
                novoLocal.desafios.Add(novoDesafio);
            }
            Locais.Add(novoLocal);
        }
    }
}