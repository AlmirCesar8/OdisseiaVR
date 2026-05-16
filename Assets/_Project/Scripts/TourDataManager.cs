using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// --- ESTRUTURAS DE DADOS PARA O RUNTIME ---
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
    public List<Desafio> desafios = new List<Desafio>();
}

// --- ESTRUTURAS DE DADOS PARA O JSON ---
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
/// RESPONSABILIDADE: Carregar dados do JSON e gerenciar o ciclo de vida dos Assets em cache.
/// Otimizado estritamente para evitar stuttering no Meta Quest 2 através de carregamento em lote.
/// </summary>
public class TourDataManager : MonoBehaviour
{
    [Header("Arquivo de Conteúdo")]
    [Tooltip("Arraste aqui o arquivo JSON que contém os dados dos tours.")]
    public TextAsset tourDataJson;

    public List<DadosLocal> Locais { get; private set; } = new List<DadosLocal>();
    public bool IsDataLoaded { get; private set; } = false;

    private void Awake()
    {
        if (tourDataJson != null)
        {
            StartCoroutine(LoadAllDataAsync());
        }
        else
        {
            Debug.LogError($"[{gameObject.name}] tourDataJson não foi atribuído no Inspector!");
        }
    }

    /// <summary>
    /// Corrotina otimizada: Dispara as requisições assíncronas em lote paralelo,
    /// evitando gargalos frame-a-frame no hardware mobile do Quest 2.
    /// </summary>
    private IEnumerator LoadAllDataAsync()
    {
        TourDataJson dadosJson = JsonUtility.FromJson<TourDataJson>(tourDataJson.text);
        Locais = new List<DadosLocal>(dadosJson.locais.Count);

        foreach (var localJson in dadosJson.locais)
        {
            DadosLocal novoLocal = new DadosLocal { locationName = localJson.locationName };

            // 1. Carregamento Assíncrono da Música de Fundo
            if (!string.IsNullOrEmpty(localJson.backgroundMusicPath))
            {
                ResourceRequest musicRequest = Resources.LoadAsync<AudioClip>(localJson.backgroundMusicPath);
                yield return musicRequest;
                novoLocal.backgroundMusic = musicRequest.asset as AudioClip;
            }

            // 2. Preparação do lote de requisições de materiais para evitar stuttering
            int numDesafios = localJson.desafios.Count;
            ResourceRequest[] requestsLote = new ResourceRequest[numDesafios];
            List<Desafio> desafiosPreparados = new List<Desafio>(numDesafios);

            // Dispara todas as requisições de leitura de disco de forma paralela no background thread da Unity
            for (int i = 0; i < numDesafios; i++)
            {
                var desJson = localJson.desafios[i];
                if (!string.IsNullOrEmpty(desJson.panoramaMaterialPath))
                {
                    requestsLote[i] = Resources.LoadAsync<Material>(desJson.panoramaMaterialPath);
                }
            }

            // Aguarda o subsistema de I/O processar as texturas/materiais pesados de 360 graus
            for (int i = 0; i < numDesafios; i++)
            {
                if (requestsLote[i] != null)
                {
                    yield return requestsLote[i];
                }
            }

            // Monta as instâncias de Runtime sem gerar sobrecarga sequencial
            for (int i = 0; i < numDesafios; i++)
            {
                var desJson = localJson.desafios[i];
                Desafio novoDesafio = new Desafio
                {
                    initialYRotation = desJson.initialYRotation,
                    questionText = desJson.questionText,
                    answers = new List<string>(desJson.answers),
                    correctAnswerIndex = desJson.correctAnswerIndex,
                    panoramaMaterial = (requestsLote[i] != null) ? requestsLote[i].asset as Material : null
                };

                if (novoDesafio.panoramaMaterial == null && !string.IsNullOrEmpty(desJson.panoramaMaterialPath))
                {
                    Debug.LogWarning($"[TourDataManager] Material inválido em: Resources/{desJson.panoramaMaterialPath}");
                }

                desafiosPreparados.Add(novoDesafio);
            }

            novoLocal.desafios.AddRange(desafiosPreparados);
            Locais.Add(novoLocal);
        }

        IsDataLoaded = true;
        Debug.Log("[TourDataManager] Carga de Assets em lote concluída com sucesso para Realidade Virtual.");
    }

    /// <summary>
    /// Abordagem de gerenciamento de memória imperativa (Proativa para VR).
    /// Deve ser chamada ao descarregar a cena para evitar vazamento de memória RAM/VRAM.
    /// </summary>
    public void LimparAssetsCarregados()
    {
        Locais.Clear();
        IsDataLoaded = false;
        // Força a Unity a liberar do chip gráfico texturas e materiais sem referência ativa
        Resources.UnloadUnusedAssets();
        System.GC.Collect();
    }

    private void OnDestroy()
    {
        LimparAssetsCarregados();
    }
}