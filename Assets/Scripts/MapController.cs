using System.Collections.Generic;
using GrandStrategy.Database;
using GrandStrategy.Database.Models;
using UnityEngine;

namespace GrandStrategy.Map
{
    /// <summary>
    /// 2D Qlobal Xəritədəki əyalətləri (Provinces) DatabaseManager-dən oxuyaraq
    /// ProvinceNode prefabları şəklində səhnəyə yerləşdirən və idarə edən menecer.
    /// </summary>
    public class MapController : MonoBehaviour
    {
        private static MapController _instance;
        public static MapController Instance => _instance != null ? _instance : (_instance = FindAnyObjectByType<MapController>());

        [Header("Xəritə və Prefab Konfiqurasiyası")]
        [Tooltip("Əyalətləri təmsil edən ProvinceNode prefabı")]
        [SerializeField] private ProvinceNode provinceNodePrefab;

        [Tooltip("Node-ların yerləşdiriləcəyi ana konteyner Transform")]
        [SerializeField] private Transform nodesContainer;

        [Tooltip("İlk əyalət node-unun başlanğıc 2D mövqeyi")]
        [SerializeField] private Vector2 startPosition = new Vector2(-2.6f, 0.2f);

        [Tooltip("Yan-yana düzülən əyalət node-ları arasındakı üfüqi məsafə")]
        [SerializeField] private float horizontalSpacing = 5.2f;

        [Header("Aktiv Əyalət Node-ları")]
        [SerializeField] private List<ProvinceNode> spawnedNodes = new List<ProvinceNode>();

        private ProvinceNode _currentlySelectedNode;

        public IReadOnlyList<ProvinceNode> SpawnedNodes => spawnedNodes;
        public ProvinceNode CurrentlySelectedNode => _currentlySelectedNode;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        private void Start()
        {
            SpawnProvincesFromDatabase();
        }

        /// <summary>
        /// DatabaseManager-dən bütün əyalətləri (Provinces) oxuyur və xəritədə yan-yana
        /// ProvinceNode prefablarını yerləşdirir.
        /// </summary>
        public void SpawnProvincesFromDatabase()
        {
            DatabaseManager db = DatabaseManager.Instance != null
                ? DatabaseManager.Instance
                : FindAnyObjectByType<DatabaseManager>();

            if (db == null)
            {
                Debug.LogError("[MapController] DatabaseManager tapılmadı!");
                return;
            }

            if (!db.IsConnected)
            {
                db.InitializeDatabase();
            }

            if (nodesContainer == null)
            {
                nodesContainer = transform;
            }

            // Əgər səhnədə artıq əvvəlcədən yaradılmış node-lar varsa, təkrar yaratmamaq üçün təmizləyirik
            ClearExistingNodes();

            List<Province> provinces = db.Connection.Table<Province>().ToList();
            if (provinces.Count == 0)
            {
                db.SeedAndTestDummyData(forceReseed: false);
                provinces = db.Connection.Table<Province>().ToList();
            }

            for (int i = 0; i < provinces.Count; i++)
            {
                Province prov = provinces[i];
                Vector3 pos = new Vector3(startPosition.x + (i * horizontalSpacing), startPosition.y, 0f);

                ProvinceNode nodeInstance;
                if (provinceNodePrefab != null)
                {
                    nodeInstance = Instantiate(provinceNodePrefab, pos, Quaternion.identity, nodesContainer);
                }
                else
                {
                    var fallbackGo = new GameObject($"ProvinceNode_{prov.Name}");
                    fallbackGo.transform.SetParent(nodesContainer, false);
                    fallbackGo.transform.position = pos;
                    nodeInstance = fallbackGo.AddComponent<ProvinceNode>();
                }

                nodeInstance.Setup(prov);
                spawnedNodes.Add(nodeInstance);
            }

            Debug.Log($"[MapController] Bazadan {spawnedNodes.Count} əyalət oxundu və 2D xəritədə yerləşdirildi.");
        }

        /// <summary>
        /// Verilmiş əyalət ID-sinə uyğun ProvinceNode-u tapır.
        /// </summary>
        public ProvinceNode GetNodeByProvinceId(int provinceId)
        {
            foreach (var node in spawnedNodes)
            {
                if (node != null && node.ProvinceId == provinceId)
                {
                    return node;
                }
            }

            return null;
        }

        /// <summary>
        /// Əyalət adına görə ProvinceNode-u tapır (məs: "Anadolu", "Şirvan").
        /// </summary>
        public ProvinceNode GetNodeByProvinceName(string provinceName)
        {
            foreach (var node in spawnedNodes)
            {
                if (node != null && string.Equals(node.ProvinceName, provinceName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return node;
                }
            }

            return null;
        }

        public void OnProvinceNodeSelected(ProvinceNode selectedNode)
        {
            _currentlySelectedNode = selectedNode;
            foreach (var node in spawnedNodes)
            {
                if (node != null)
                {
                    node.SetSelected(node == selectedNode);
                }
            }
        }

        private void ClearExistingNodes()
        {
            spawnedNodes.Clear();
            if (nodesContainer == null)
            {
                return;
            }

            for (int i = nodesContainer.childCount - 1; i >= 0; i--)
            {
                Transform child = nodesContainer.GetChild(i);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }
    }
}
