using UnityEngine;
using UnityEngine.UI;

// Bir container'ı hem YATAY hem DİKEY olarak (grid şeklinde) sıralamak için
// kullanılır. Oge sayısı değiştikçe (biri eklenince/çıkınca) hücreleri KARE
// tutarak, hepsi container'ın sınırlarına sığacak şekilde otomatik yeniden
// boyutlandırır. Lobideki oyuncu listesi ve oylama panelindeki oyuncu
// butonları container'larının ikisine de eklenebilir.
//
// Kullanım: Bu scripti, üzerinde GridLayoutGroup bulunan container objesine
// ekleyin (RectTransform zaten Unity UI objelerinde vardır). Başka hiçbir
// koda dokunmanıza gerek yok; container'ın child'ları her değiştiğinde
// (Instantiate/Destroy) devreye girer.
[RequireComponent(typeof(GridLayoutGroup))]
[RequireComponent(typeof(RectTransform))]
public class GridAutoLayout : MonoBehaviour
{
    [Header("Ayarlar")]
    [Tooltip("Bir satırda/sütunda olabilecek en fazla eleman sayısı. " +
             "Ekrana göre makul bir üst sınır verin (örn. 6-8).")]
    [SerializeField] private int maxColumns = 6;

    [Tooltip("Hücreler arası boşluk (piksel).")]
    [SerializeField] private float spacing = 10f;

    private GridLayoutGroup grid;
    private RectTransform rectTransform;

    private void Awake()
    {
        grid = GetComponent<GridLayoutGroup>();
        rectTransform = GetComponent<RectTransform>();
    }

    private void OnEnable()
    {
        Recalculate();
    }

    // Unity, bu objenin child'ları her değiştiğinde (ekleme/çıkarma) bu mesajı
    // otomatik gönderir; LobbyPanel veya VotePanel'in ayrıca bir şey çağırmasına
    // gerek yoktur.
    private void OnTransformChildrenChanged()
    {
        Recalculate();
    }

    private void Recalculate()
    {
        int itemCount = transform.childCount;
        if (itemCount <= 0 || grid == null || rectTransform == null) return;

        // Kare bir grid için sütun sayısını sayının kareköküne yuvarlayarak buluyoruz;
        // böylece hem yatayda hem dikeyde dengeli bir dağılım oluyor.
        int columns = Mathf.CeilToInt(Mathf.Sqrt(itemCount));
        columns = Mathf.Clamp(columns, 1, maxColumns);
        int rows = Mathf.CeilToInt((float)itemCount / columns);

        grid.spacing = new Vector2(spacing, spacing);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;

        float containerWidth = rectTransform.rect.width
                              - grid.padding.left - grid.padding.right
                              - spacing * (columns - 1);

        float containerHeight = rectTransform.rect.height
                               - grid.padding.top - grid.padding.bottom
                               - spacing * (rows - 1);

        float cellWidth = containerWidth / columns;
        float cellHeight = containerHeight / rows;

        // Kare hücre: ikisinden küçük olanı seçiyoruz ki hiçbir eleman taşmasın.
        float cellSize = Mathf.Max(1f, Mathf.Min(cellWidth, cellHeight));

        grid.cellSize = new Vector2(cellSize, cellSize);
    }
}
