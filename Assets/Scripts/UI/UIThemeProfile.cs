using System;
using TMPro;
using UnityEngine;

namespace GrandStrategy.UI
{
    /// <summary>
    /// AI qrafik assetlərini (taxta panellər, naxışlı çərçivələr, düymələr, xüsusi şriftlər və rəng palitrası)
    /// mərkəzləşdirilmiş şəkildə saxlayan və asan dəyişdirilməyə imkan verən UI Tema Profili (ScriptableObject).
    /// </summary>
    [CreateAssetMenu(fileName = "NewUIThemeProfile", menuName = "Grand Strategy/UI/Theme Profile", order = 100)]
    public class UIThemeProfile : ScriptableObject
    {
        [Header("AI Qrafik Assetləri (Spritlər)")]
        [Tooltip("Panellərin arxa fonu üçün istifadə ediləcək sprite (taxta, dəri və ya perqament fakturası)")]
        public Sprite panelBackground;

        [Tooltip("Panellərin kənar çərçivələri və bəzəkləri üçün dekorativ sprite")]
        public Sprite borderDecoration;

        [Tooltip("Standart düymələrin arxa planı üçün sprite")]
        public Sprite buttonNormal;

        [Header("Rəng Palitrası")]
        [Tooltip("Əsas haşiyə və kənar xətt rəngi (Klassik qızılı #C5A059)")]
        public Color primaryBorderColor = new Color(0.773f, 0.627f, 0.349f, 1f); // #C5A059

        [Tooltip("Panellərin əsas arxa fon rəngi (Tünd ~88% Alpha #111111)")]
        public Color panelBackgroundColor = new Color(0.067f, 0.067f, 0.067f, 0.88f); // #111111 ~88% Alpha

        [Tooltip("İkinci dərəcəli başlıq zolağı fon rəngi")]
        public Color secondaryHeaderColor = new Color(0.11f, 0.13f, 0.17f, 0.95f);

        [Tooltip("Qızılı/vurğu mətn rəngi (#F1C40F)")]
        public Color goldTextColor = new Color(0.945f, 0.769f, 0.059f, 1f);

        [Header("Şriftlər (Typography)")]
        [Tooltip("Bütün standart mətnlər, statistikalar və salnamə sətirləri üçün əsas şrift")]
        public TMP_FontAsset mainFont;

        [Tooltip("Panel başlıqları və dövlət adları üçün xüsusi başlıq şrifti")]
        public TMP_FontAsset titleFont;
    }
}
