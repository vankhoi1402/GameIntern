using UnityEngine;
using UnityEngine.UI;

/// <summary>Helpers bind nút cho popup Layer Lab / UI legacy.</summary>
public static class PopupUIButtonUtility
{
    private const float c_LayerLabButtonBottomOffset = 140f;

    public static Transform FindChildDeep(Transform parent, string childName)
    {
        if (parent == null)
            return null;

        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
                return child;
        }

        return null;
    }

    public static bool UsesLayerLabButton(Transform root)
    {
        return root != null && FindChildDeep(root, "Button_124_Blue") != null;
    }

    public static Button EnsureButton(Transform root, string buttonName, Button cached)
    {
        Button button = cached;

        if (button == null)
        {
            Transform buttonTransform = FindChildDeep(root, buttonName);
            if (buttonTransform == null)
                return null;

            if (!buttonTransform.TryGetComponent(out button))
            {
                Image image = buttonTransform.GetComponent<Image>();
                button = buttonTransform.gameObject.AddComponent<Button>();
                if (image != null)
                    button.targetGraphic = image;
            }
        }

        if (button == null)
            return null;

        PrepareLayerLabPopup(root, buttonName);
        ConfigureButtonRaycasts(button);
        return button;
    }

    public static void PrepareLayerLabPopup(Transform root, string buttonName)
    {
        if (root == null)
            return;

        DisableOverlayRaycasts(root);

        Transform buttonTransform = FindChildDeep(root, buttonName);
        if (buttonTransform == null)
            return;

        buttonTransform.SetAsLastSibling();

        if (buttonTransform is RectTransform rect)
        {
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, c_LayerLabButtonBottomOffset);
        }
    }

    private static void DisableOverlayRaycasts(Transform root)
    {
        Transform dimed = FindChildDeep(root, "Dimed");
        if (dimed == null || !dimed.TryGetComponent<Graphic>(out Graphic graphic))
            return;

        graphic.raycastTarget = false;
    }

    private static void ConfigureButtonRaycasts(Button button)
    {
        button.navigation = new Navigation { mode = Navigation.Mode.None };

        if (button.targetGraphic == null && button.TryGetComponent(out Image image))
            button.targetGraphic = image;

        Graphic target = button.targetGraphic;
        if (target != null)
            target.raycastTarget = true;

        foreach (Graphic graphic in button.GetComponentsInChildren<Graphic>(true))
        {
            if (graphic != target)
                graphic.raycastTarget = false;
        }
    }
}
