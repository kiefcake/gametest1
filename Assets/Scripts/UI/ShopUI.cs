using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DungeonCrawler.Core;
using DungeonCrawler.Inventory;
using DungeonCrawler.Visuals;
using DungeonCrawler.World;
using DungeonCrawler.Audio;

namespace DungeonCrawler.UI
{
    // Vendor buy panel -- one shared instance reused by every VendorNPC (see
    // GameBootstrap.WireVendors, which wires each vendor's Interactable.onInteract to
    // Show()). Unlocks the cursor while open: Unity's UI input module won't route clicks
    // to a Button while Cursor.lockState is Locked (see PointerInputModule.Process), the
    // same fix InventoryUI needed and previously didn't have.
    //
    // Also doubles as a sell panel (any vendor buys back any of your own loot, at a
    // markdown -- see ItemEconomy) via a Buy/Sell tab toggle, rather than needing a
    // separate dedicated "pawnbroker" UI for what's otherwise the exact same row-list-plus-
    // price-button layout.
    public class ShopUI : MonoBehaviour
    {
        private static ShopUI instance;

        private GameObject panelRoot;
        private Text titleText;
        private Text flavorText;
        private Text goldText;
        private Transform rowParent;
        private RectTransform rowsContentRect;
        private Font font;
        private Button buyTabButton;
        private Button sellTabButton;

        private VendorNPC vendor;
        private InventorySystem inventory;
        private PlayerWallet wallet;
        private Sprite rowBgSprite;
        private Sprite buttonSprite;
        private bool sellMode;

        private readonly List<GameObject> rowObjects = new List<GameObject>();

        private static readonly Color PanelFill = new Color(0.08f, 0.08f, 0.11f, 0.97f);
        private static readonly Color PanelBorder = new Color(0.55f, 0.45f, 0.25f, 1f); // warm gold -- a shop, not a dungeon menu
        private static readonly Color RowFill = new Color(0.16f, 0.16f, 0.2f, 0.9f);
        private static readonly Color RowBorder = new Color(0.3f, 0.3f, 0.36f, 1f);
        private static readonly Color TabActive = new Color(0.35f, 0.75f, 0.4f);
        private static readonly Color TabInactive = new Color(0.24f, 0.24f, 0.28f);

        public static void Show(VendorNPC vendor, InventorySystem inventory, PlayerWallet wallet)
        {
            if (instance == null)
            {
                var go = new GameObject("ShopUI");
                instance = go.AddComponent<ShopUI>();
                instance.BuildUI();
            }
            instance.Open(vendor, inventory, wallet);
        }

        private void Open(VendorNPC v, InventorySystem inv, PlayerWallet w)
        {
            if (wallet != null) wallet.OnChanged -= Redraw;
            vendor = v;
            inventory = inv;
            wallet = w;
            if (wallet != null) wallet.OnChanged += Redraw;

            sellMode = false;
            panelRoot.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Redraw();
        }

        private void Close()
        {
            panelRoot.SetActive(false);
            if (wallet != null) wallet.OnChanged -= Redraw;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void SetMode(bool sell)
        {
            if (sellMode == sell) return;
            sellMode = sell;
            Redraw();
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("ShopCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90; // above the HUD (default order), below the Pause menu (100)
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            panelRoot = new GameObject("ShopPanel", typeof(RectTransform), typeof(Image));
            panelRoot.transform.SetParent(canvasGO.transform, false);
            var panelRect = panelRoot.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(680, 640);
            var panelImage = panelRoot.GetComponent<Image>();
            panelImage.sprite = PanelSpriteFactory.CreateRoundedSprite(PanelFill, PanelBorder);
            panelImage.type = Image.Type.Sliced;
            panelImage.color = Color.white;

            rowBgSprite = PanelSpriteFactory.CreateRoundedSprite(RowFill, RowBorder, size: 64, radius: 10, borderThickness: 3);
            buttonSprite = PanelSpriteFactory.CreateRoundedSprite(new Color(0.2f, 0.2f, 0.24f), new Color(0.4f, 0.4f, 0.48f), size: 64, radius: 10, borderThickness: 3);

            titleText = MakeLabel(panelRoot.transform, 28, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0, -36), new Vector2(560, 42), Color.white);
            flavorText = MakeLabel(panelRoot.transform, 15, FontStyle.Italic, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0, -70), new Vector2(620, 28), new Color(0.7f, 0.7f, 0.75f));
            goldText = MakeLabel(panelRoot.transform, 20, FontStyle.Bold, TextAnchor.MiddleRight,
                new Vector2(1f, 1f), new Vector2(-28, -36), new Vector2(180, 34), new Color(1f, 0.84f, 0.2f));

            buyTabButton = MakeTabButton(panelRoot.transform, "Buy", new Vector2(-72, -102), () => SetMode(false));
            sellTabButton = MakeTabButton(panelRoot.transform, "Sell", new Vector2(72, -102), () => SetMode(true));

            // A masked scroll view instead of the old bare VerticalLayoutGroup -- vendor
            // stock tops out around 8 rows, but Sell mode can list up to a full 20-slot
            // inventory, which would otherwise spill silently off the bottom of the panel
            // with no way to reach the rest.
            //
            // RectMask2D, not Mask+Image: Mask clips via an alpha test on its Graphic, and
            // the near-invisible background color this used (alpha 0.001, to stay
            // invisible) sat right at or under Unity's alpha-clip threshold -- clipping
            // every row regardless of content, which is exactly what made every vendor's
            // stock (and the Sell tab's own inventory list) look empty. RectMask2D clips by
            // rect bounds alone, needs no Graphic and no alpha, and is what Unity's own
            // Scroll View pattern uses for plain rectangular clipping like this.
            var scrollGO = new GameObject("RowsScroll", typeof(RectTransform), typeof(RectMask2D), typeof(ScrollRect));
            scrollGO.transform.SetParent(panelRoot.transform, false);
            var scrollRect = scrollGO.GetComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0f, 0f);
            scrollRect.anchorMax = new Vector2(1f, 1f);
            scrollRect.offsetMin = new Vector2(20, 76);
            scrollRect.offsetMax = new Vector2(-20, -140);

            var rowsGO = new GameObject("Rows", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            rowsGO.transform.SetParent(scrollGO.transform, false);
            rowsContentRect = rowsGO.GetComponent<RectTransform>();
            rowsContentRect.anchorMin = new Vector2(0f, 1f);
            rowsContentRect.anchorMax = new Vector2(1f, 1f);
            rowsContentRect.pivot = new Vector2(0.5f, 1f);
            rowsContentRect.anchoredPosition = Vector2.zero;
            rowsContentRect.sizeDelta = Vector2.zero;
            var layoutGroup = rowsGO.GetComponent<VerticalLayoutGroup>();
            layoutGroup.spacing = 10;
            layoutGroup.childControlHeight = false;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childControlWidth = true;
            layoutGroup.childForceExpandWidth = true;
            var fitter = rowsGO.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            rowParent = rowsGO.transform;

            var scroll = scrollGO.GetComponent<ScrollRect>();
            scroll.content = rowsContentRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;

            BuildButton(panelRoot.transform, "Close", new Vector2(0, 30), Close);

            panelRoot.SetActive(false);
        }

        private void Redraw()
        {
            if (vendor == null) return;
            titleText.text = vendor.vendorName;
            flavorText.text = sellMode
                ? "Sell your own loot -- vendors pay less than they charge for it."
                : vendor.flavorText;
            goldText.text = wallet != null ? $"{wallet.Gold}g" : "0g";

            buyTabButton.GetComponent<Image>().color = sellMode ? TabInactive : TabActive;
            sellTabButton.GetComponent<Image>().color = sellMode ? TabActive : TabInactive;

            foreach (var row in rowObjects) Destroy(row);
            rowObjects.Clear();

            if (sellMode)
            {
                if (inventory != null)
                {
                    for (int i = 0; i < inventory.SlotCount; i++)
                    {
                        var item = inventory.GetAt(i);
                        if (item == null) continue;
                        rowObjects.Add(BuildSellRow(i, item));
                    }
                }
                if (rowObjects.Count == 0) rowObjects.Add(BuildEmptyRow("Nothing in your bag worth selling."));
            }
            else
            {
                if (vendor.stock != null)
                {
                    foreach (var entry in vendor.stock)
                    {
                        if (entry.item == null) continue;
                        rowObjects.Add(BuildRow(entry));
                    }
                }
            }
        }

        private GameObject BuildRow(ShopStock entry)
        {
            var rowGO = BuildRowBase("Row_" + entry.item.itemName, entry.item);

            bool canAfford = wallet != null && wallet.Gold >= entry.price;
            var capturedEntry = entry; // capture per-row value, not the loop variable
            BuildPriceButton(rowGO.transform, $"{entry.price}g", canAfford,
                canAfford ? new Color(0.35f, 0.75f, 0.4f) : new Color(0.6f, 0.3f, 0.3f),
                () => Buy(capturedEntry));

            return rowGO;
        }

        private GameObject BuildSellRow(int index, ItemData item)
        {
            var rowGO = BuildRowBase("SellRow_" + item.itemName, item);

            int price = ItemEconomy.SellPrice(item);
            int capturedIndex = index; // capture per-row value, not the loop variable
            BuildPriceButton(rowGO.transform, $"Sell {price}g", true, new Color(0.55f, 0.45f, 0.25f),
                () => SellItem(capturedIndex));

            return rowGO;
        }

        private GameObject BuildEmptyRow(string message)
        {
            var rowGO = new GameObject("EmptyRow", typeof(RectTransform), typeof(Image));
            rowGO.transform.SetParent(rowParent, false);
            rowGO.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 66);
            var rowImage = rowGO.GetComponent<Image>();
            rowImage.sprite = rowBgSprite;
            rowImage.type = Image.Type.Sliced;
            rowImage.color = Color.white;

            var text = MakeLabel(rowGO.transform, 15, FontStyle.Italic, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560, 52), new Color(0.6f, 0.6f, 0.65f));
            text.text = message;
            return rowGO;
        }

        // Shared icon/rarity-backdrop/name layout used by both the buy and sell rows --
        // only the trailing price button differs between the two, built by the caller.
        private GameObject BuildRowBase(string name, ItemData item)
        {
            var rowGO = new GameObject(name, typeof(RectTransform), typeof(Image));
            rowGO.transform.SetParent(rowParent, false);
            rowGO.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 66);
            var rowImage = rowGO.GetComponent<Image>();
            rowImage.sprite = rowBgSprite;
            rowImage.type = Image.Type.Sliced;
            rowImage.color = Color.white;

            // Invisible container -- just a raycast target so HoverTooltip (below) can
            // receive pointer events; Unity's GraphicRaycaster only hits Graphic components,
            // so this needs an Image even though it shows nothing itself.
            var iconGO = new GameObject("IconContainer", typeof(RectTransform), typeof(Image));
            iconGO.transform.SetParent(rowGO.transform, false);
            var iconRect = iconGO.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0, 0.5f);
            iconRect.anchorMax = new Vector2(0, 0.5f);
            iconRect.anchoredPosition = new Vector2(38, 0);
            iconRect.sizeDelta = new Vector2(52, 52);
            iconGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

            // Rarity-colored backdrop behind the icon, same convention as InventoryUI's
            // slots (see RarityColors) -- inserted first so the icon (added after) renders
            // on top of it.
            var rarityGO = new GameObject("RarityBg", typeof(RectTransform), typeof(Image));
            rarityGO.transform.SetParent(iconGO.transform, false);
            var rarityRect = rarityGO.GetComponent<RectTransform>();
            rarityRect.anchorMin = new Vector2(0.08f, 0.08f);
            rarityRect.anchorMax = new Vector2(0.92f, 0.92f);
            rarityRect.offsetMin = Vector2.zero;
            rarityRect.offsetMax = Vector2.zero;
            rarityGO.GetComponent<Image>().color = RarityColors.Get(item.rarity);

            var iconImgGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconImgGO.transform.SetParent(iconGO.transform, false);
            var iconImgRect = iconImgGO.GetComponent<RectTransform>();
            iconImgRect.anchorMin = Vector2.zero;
            iconImgRect.anchorMax = Vector2.one;
            iconImgRect.offsetMin = Vector2.zero;
            iconImgRect.offsetMax = Vector2.zero;
            var icon = iconImgGO.GetComponent<Image>();
            icon.sprite = item.icon;
            icon.enabled = item.icon != null;

            var tooltip = iconGO.AddComponent<HoverTooltip>();
            string tooltipHeader = $"{RarityColors.Label(item.rarity)} {item.itemName}{(item.isUnique ? " UT" : "")}";
            tooltip.content = string.IsNullOrEmpty(item.description)
                ? tooltipHeader
                : $"{tooltipHeader}\n{item.description}";

            var nameGO = new GameObject("Name", typeof(RectTransform), typeof(Text));
            nameGO.transform.SetParent(rowGO.transform, false);
            var nameRect = nameGO.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 0.5f);
            nameRect.anchorMax = new Vector2(0, 0.5f);
            // Pivot has to match the anchor (left-middle), or this defaults to (0.5, 0.5)
            // and the box centers on x=76 instead of starting there -- half its 320 width
            // then spills left past the panel's own edge, out into the game world behind it.
            nameRect.pivot = new Vector2(0, 0.5f);
            nameRect.anchoredPosition = new Vector2(76, 0);
            nameRect.sizeDelta = new Vector2(300, 52);
            var nameText = nameGO.GetComponent<Text>();
            nameText.font = font;
            nameText.fontSize = 17;
            nameText.alignment = TextAnchor.MiddleLeft;
            nameText.color = RarityColors.Get(item.rarity);
            nameText.text = item.itemName + (item.isUnique ? " UT" : "");

            return rowGO;
        }

        private void BuildPriceButton(Transform rowTransform, string label, bool interactable, Color color, UnityEngine.Events.UnityAction onClick)
        {
            var buyGO = new GameObject("Price", typeof(RectTransform), typeof(Image), typeof(Button));
            buyGO.transform.SetParent(rowTransform, false);
            var buyRect = buyGO.GetComponent<RectTransform>();
            buyRect.anchorMin = new Vector2(1, 0.5f);
            buyRect.anchorMax = new Vector2(1, 0.5f);
            buyRect.anchoredPosition = new Vector2(-20, 0);
            buyRect.sizeDelta = new Vector2(120, 48);
            var buyImage = buyGO.GetComponent<Image>();
            buyImage.sprite = buttonSprite;
            buyImage.type = Image.Type.Sliced;
            buyImage.color = color;
            var button = buyGO.GetComponent<Button>();
            button.interactable = interactable;
            button.onClick.AddListener(onClick);

            var buyTextGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
            buyTextGO.transform.SetParent(buyGO.transform, false);
            var buyTextRect = buyTextGO.GetComponent<RectTransform>();
            buyTextRect.anchorMin = Vector2.zero;
            buyTextRect.anchorMax = Vector2.one;
            buyTextRect.offsetMin = Vector2.zero;
            buyTextRect.offsetMax = Vector2.zero;
            var buyText = buyTextGO.GetComponent<Text>();
            buyText.font = font;
            buyText.fontSize = 15;
            buyText.alignment = TextAnchor.MiddleCenter;
            buyText.color = Color.white;
            buyText.text = label;
        }

        // wallet.Spend/Add both fire OnChanged, which is subscribed straight to Redraw --
        // no manual redraw call needed here, success or failure.
        private void Buy(ShopStock entry)
        {
            if (wallet == null || inventory == null || entry.item == null) return;
            if (!wallet.Spend(entry.price)) return;
            if (!inventory.AddItem(entry.item)) wallet.Add(entry.price); // inventory full -- refund
        }

        private void SellItem(int index)
        {
            if (wallet == null || inventory == null) return;
            var item = inventory.GetAt(index);
            if (item == null) return;

            int price = ItemEconomy.SellPrice(item);
            inventory.RemoveAt(index);
            wallet.Add(price);
            SfxLibrary.PlayAt(SfxLibrary.Gold, transform.position, 0.3f);
            Redraw();
        }

        private Text MakeLabel(Transform parent, int fontSize, FontStyle style, TextAnchor anchor,
            Vector2 anchorPoint, Vector2 anchoredPos, Vector2 sizeDelta, Color color)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorPoint;
            rect.anchorMax = anchorPoint;
            rect.pivot = anchorPoint;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = sizeDelta;
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = anchor;
            text.color = color;
            return text;
        }

        private void BuildButton(Transform parent, string label, Vector2 anchoredPos, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(label + "Button", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(220, 48);
            var img = go.GetComponent<Image>();
            img.sprite = buttonSprite;
            img.type = Image.Type.Sliced;
            img.color = Color.white;

            var textGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGO.transform.SetParent(go.transform, false);
            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textGO.GetComponent<Text>();
            text.font = font;
            text.fontSize = 18;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = label;

            go.GetComponent<Button>().onClick.AddListener(onClick);
        }

        private Button MakeTabButton(Transform parent, string label, Vector2 anchoredPos, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(label + "Tab", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(130, 32);
            var img = go.GetComponent<Image>();
            img.sprite = buttonSprite;
            img.type = Image.Type.Sliced;
            img.color = TabInactive;

            var textGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGO.transform.SetParent(go.transform, false);
            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textGO.GetComponent<Text>();
            text.font = font;
            text.fontSize = 15;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = label;

            var button = go.GetComponent<Button>();
            button.onClick.AddListener(onClick);
            return button;
        }
    }
}
