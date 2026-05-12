using System.Collections.Generic;

namespace SimpleTweaks
{
    // Hardcoded translations for all SimpleTweaks.* keys.
    // The Patch_LEManager_Get_CustomKeys prefix intercepts LEManager.Get calls for
    // keys starting with "SimpleTweaks." and returns the appropriate locale string.
    internal static class LocalisationData
    {
        private static readonly Dictionary<string, Dictionary<string, string>> _translations =
            new Dictionary<string, Dictionary<string, string>>
        {
            ["en-US"] = new Dictionary<string, string>
            {
                ["SimpleTweaks.Tooltip.CtrlHint"]             = "Ctrl+Click: \u00b1100",
                ["SimpleTweaks.Tooltip.BuildShiftCtrl"]       = "Shift+Click: \u00d710\nCtrl+Click: \u00d7100",
                ["SimpleTweaks.Tooltip.AddModuleShiftCtrl"]   = "Shift+Click: add \u00d710\nCtrl+Click: add \u00d7100",
                ["SimpleTweaks.Tooltip.RemoveModuleShiftCtrl"]= "Shift+Click: remove \u00d710\nCtrl+Click: remove \u00d7100",
                ["SimpleTweaks.Tooltip.CancelAllBuildings"]   = "Shift+Click: cancel all buildings under construction on this body",
            },
            ["de-DE"] = new Dictionary<string, string>
            {
                ["SimpleTweaks.Tooltip.CtrlHint"]             = "Strg+Klick: \u00b1100",
                ["SimpleTweaks.Tooltip.BuildShiftCtrl"]       = "Shift+Klick: \u00d710\nStrg+Klick: \u00d7100",
                ["SimpleTweaks.Tooltip.AddModuleShiftCtrl"]   = "Shift+Klick: \u00d710 hinzuf\u00fcgen\nStrg+Klick: \u00d7100 hinzuf\u00fcgen",
                ["SimpleTweaks.Tooltip.RemoveModuleShiftCtrl"]= "Shift+Klick: \u00d710 entfernen\nStrg+Klick: \u00d7100 entfernen",
                ["SimpleTweaks.Tooltip.CancelAllBuildings"]   = "Shift+Klick: alle Geb\u00e4ude im Bau auf diesem K\u00f6rper abbrechen",
            },
            ["es-ES"] = new Dictionary<string, string>
            {
                ["SimpleTweaks.Tooltip.CtrlHint"]             = "Ctrl+Clic: \u00b1100",
                ["SimpleTweaks.Tooltip.BuildShiftCtrl"]       = "Shift+Clic: \u00d710\nCtrl+Clic: \u00d7100",
                ["SimpleTweaks.Tooltip.AddModuleShiftCtrl"]   = "Shift+Clic: a\u00f1adir \u00d710\nCtrl+Clic: a\u00f1adir \u00d7100",
                ["SimpleTweaks.Tooltip.RemoveModuleShiftCtrl"]= "Shift+Clic: quitar \u00d710\nCtrl+Clic: quitar \u00d7100",
                ["SimpleTweaks.Tooltip.CancelAllBuildings"]   = "Shift+Clic: cancelar todos los edificios en construcci\u00f3n en este cuerpo",
            },
            ["fr-FR"] = new Dictionary<string, string>
            {
                ["SimpleTweaks.Tooltip.CtrlHint"]             = "Ctrl+Clic: \u00b1100",
                ["SimpleTweaks.Tooltip.BuildShiftCtrl"]       = "Maj+Clic: \u00d710\nCtrl+Clic: \u00d7100",
                ["SimpleTweaks.Tooltip.AddModuleShiftCtrl"]   = "Maj+Clic: ajouter \u00d710\nCtrl+Clic: ajouter \u00d7100",
                ["SimpleTweaks.Tooltip.RemoveModuleShiftCtrl"]= "Maj+Clic: retirer \u00d710\nCtrl+Clic: retirer \u00d7100",
                ["SimpleTweaks.Tooltip.CancelAllBuildings"]   = "Maj+Clic: annuler tous les b\u00e2timents en construction sur ce corps",
            },
            ["it-IT"] = new Dictionary<string, string>
            {
                ["SimpleTweaks.Tooltip.CtrlHint"]             = "Ctrl+Clic: \u00b1100",
                ["SimpleTweaks.Tooltip.BuildShiftCtrl"]       = "Shift+Clic: \u00d710\nCtrl+Clic: \u00d7100",
                ["SimpleTweaks.Tooltip.AddModuleShiftCtrl"]   = "Shift+Clic: aggiungi \u00d710\nCtrl+Clic: aggiungi \u00d7100",
                ["SimpleTweaks.Tooltip.RemoveModuleShiftCtrl"]= "Shift+Clic: rimuovi \u00d710\nCtrl+Clic: rimuovi \u00d7100",
                ["SimpleTweaks.Tooltip.CancelAllBuildings"]   = "Shift+Clic: annulla tutti gli edifici in costruzione su questo corpo",
            },
            ["jp-JP"] = new Dictionary<string, string>
            {
                ["SimpleTweaks.Tooltip.CtrlHint"]             = "Ctrl+\u30af\u30ea\u30c3\u30af: \u00b1100",
                ["SimpleTweaks.Tooltip.BuildShiftCtrl"]       = "Shift+\u30af\u30ea\u30c3\u30af: \u00d710\nCtrl+\u30af\u30ea\u30c3\u30af: \u00d7100",
                ["SimpleTweaks.Tooltip.AddModuleShiftCtrl"]   = "Shift+\u30af\u30ea\u30c3\u30af: \u00d710 \u8ffd\u52a0\nCtrl+\u30af\u30ea\u30c3\u30af: \u00d7100 \u8ffd\u52a0",
                ["SimpleTweaks.Tooltip.RemoveModuleShiftCtrl"]= "Shift+\u30af\u30ea\u30c3\u30af: \u00d710 \u524a\u9664\nCtrl+\u30af\u30ea\u30c3\u30af: \u00d7100 \u524a\u9664",
                ["SimpleTweaks.Tooltip.CancelAllBuildings"]   = "Shift+\u30af\u30ea\u30c3\u30af: \u3053\u306e\u661f\u4f53\u3067\u5efa\u8a2d\u4e2d\u306e\u5168\u3066\u306e\u5efa\u7269\u3092\u30ad\u30e3\u30f3\u30bb\u30eb",
            },
            ["ko-KO"] = new Dictionary<string, string>
            {
                ["SimpleTweaks.Tooltip.CtrlHint"]             = "Ctrl+\ud074\ub9ad: \u00b1100",
                ["SimpleTweaks.Tooltip.BuildShiftCtrl"]       = "Shift+\ud074\ub9ad: \u00d710\nCtrl+\ud074\ub9ad: \u00d7100",
                ["SimpleTweaks.Tooltip.AddModuleShiftCtrl"]   = "Shift+\ud074\ub9ad: \u00d710 \ucd94\uac00\nCtrl+\ud074\ub9ad: \u00d7100 \ucd94\uac00",
                ["SimpleTweaks.Tooltip.RemoveModuleShiftCtrl"]= "Shift+\ud074\ub9ad: \u00d710 \uc81c\uac70\nCtrl+\ud074\ub9ad: \u00d7100 \uc81c\uac70",
                ["SimpleTweaks.Tooltip.CancelAllBuildings"]   = "Shift+\ud074\ub9ad: \uc774 \ucc9c\uccb4\uc758 \uac74\uc124 \uc911\uc778 \ubaa8\ub4e0 \uac74\ubb3c \ucde8\uc18c",
            },
            ["pl-PL"] = new Dictionary<string, string>
            {
                ["SimpleTweaks.Tooltip.CtrlHint"]             = "Ctrl+klik: \u00b1100",
                ["SimpleTweaks.Tooltip.BuildShiftCtrl"]       = "Shift+klik: \u00d710\nCtrl+klik: \u00d7100",
                ["SimpleTweaks.Tooltip.AddModuleShiftCtrl"]   = "Shift+klik: dodaj \u00d710\nCtrl+klik: dodaj \u00d7100",
                ["SimpleTweaks.Tooltip.RemoveModuleShiftCtrl"]= "Shift+klik: usu\u0144 \u00d710\nCtrl+klik: usu\u0144 \u00d7100",
                ["SimpleTweaks.Tooltip.CancelAllBuildings"]   = "Shift+klik: anuluj wszystkie budynki w budowie na tym ciele",
            },
            ["pt-BR"] = new Dictionary<string, string>
            {
                ["SimpleTweaks.Tooltip.CtrlHint"]             = "Ctrl+Clique: \u00b1100",
                ["SimpleTweaks.Tooltip.BuildShiftCtrl"]       = "Shift+Clique: \u00d710\nCtrl+Clique: \u00d7100",
                ["SimpleTweaks.Tooltip.AddModuleShiftCtrl"]   = "Shift+Clique: adicionar \u00d710\nCtrl+Clique: adicionar \u00d7100",
                ["SimpleTweaks.Tooltip.RemoveModuleShiftCtrl"]= "Shift+Clique: remover \u00d710\nCtrl+Clique: remover \u00d7100",
                ["SimpleTweaks.Tooltip.CancelAllBuildings"]   = "Shift+Clique: cancelar todos os edif\u00edcios em constru\u00e7\u00e3o neste corpo",
            },
            ["pt-PT"] = new Dictionary<string, string>
            {
                ["SimpleTweaks.Tooltip.CtrlHint"]             = "Ctrl+Clique: \u00b1100",
                ["SimpleTweaks.Tooltip.BuildShiftCtrl"]       = "Shift+Clique: \u00d710\nCtrl+Clique: \u00d7100",
                ["SimpleTweaks.Tooltip.AddModuleShiftCtrl"]   = "Shift+Clique: adicionar \u00d710\nCtrl+Clique: adicionar \u00d7100",
                ["SimpleTweaks.Tooltip.RemoveModuleShiftCtrl"]= "Shift+Clique: remover \u00d710\nCtrl+Clique: remover \u00d7100",
                ["SimpleTweaks.Tooltip.CancelAllBuildings"]   = "Shift+Clique: cancelar todos os edif\u00edcios em constru\u00e7\u00e3o neste corpo",
            },
            ["ru-RU"] = new Dictionary<string, string>
            {
                ["SimpleTweaks.Tooltip.CtrlHint"]             = "Ctrl+\u041a\u043b\u0438\u043a: \u00b1100",
                ["SimpleTweaks.Tooltip.BuildShiftCtrl"]       = "Shift+\u041a\u043b\u0438\u043a: \u00d710\nCtrl+\u041a\u043b\u0438\u043a: \u00d7100",
                ["SimpleTweaks.Tooltip.AddModuleShiftCtrl"]   = "Shift+\u041a\u043b\u0438\u043a: \u0434\u043e\u0431\u0430\u0432\u0438\u0442\u044c \u00d710\nCtrl+\u041a\u043b\u0438\u043a: \u0434\u043e\u0431\u0430\u0432\u0438\u0442\u044c \u00d7100",
                ["SimpleTweaks.Tooltip.RemoveModuleShiftCtrl"]= "Shift+\u041a\u043b\u0438\u043a: \u0443\u0434\u0430\u043b\u0438\u0442\u044c \u00d710\nCtrl+\u041a\u043b\u0438\u043a: \u0443\u0434\u0430\u043b\u0438\u0442\u044c \u00d7100",
                ["SimpleTweaks.Tooltip.CancelAllBuildings"]   = "Shift+\u041a\u043b\u0438\u043a: \u043e\u0442\u043c\u0435\u043d\u0438\u0442\u044c \u0432\u0441\u0435 \u0441\u0442\u0440\u043e\u044f\u0449\u0438\u0435\u0441\u044f \u0437\u0434\u0430\u043d\u0438\u044f",
            },
            ["tr-TR"] = new Dictionary<string, string>
            {
                ["SimpleTweaks.Tooltip.CtrlHint"]             = "Ctrl+T\u0131kla: \u00b1100",
                ["SimpleTweaks.Tooltip.BuildShiftCtrl"]       = "Shift+T\u0131kla: \u00d710\nCtrl+T\u0131kla: \u00d7100",
                ["SimpleTweaks.Tooltip.AddModuleShiftCtrl"]   = "Shift+T\u0131kla: \u00d710 ekle\nCtrl+T\u0131kla: \u00d7100 ekle",
                ["SimpleTweaks.Tooltip.RemoveModuleShiftCtrl"]= "Shift+T\u0131kla: \u00d710 kald\u0131r\nCtrl+T\u0131kla: \u00d7100 kald\u0131r",
                ["SimpleTweaks.Tooltip.CancelAllBuildings"]   = "Shift+T\u0131kla: bu cisimdeki yap\u0131m a\u015famas\u0131ndaki t\u00fcm binalar\u0131 iptal et",
            },
            ["zh-CN"] = new Dictionary<string, string>
            {
                ["SimpleTweaks.Tooltip.CtrlHint"]             = "Ctrl+\u70b9\u51fb: \u00b1100",
                ["SimpleTweaks.Tooltip.BuildShiftCtrl"]       = "Shift+\u70b9\u51fb: \u00d710\nCtrl+\u70b9\u51fb: \u00d7100",
                ["SimpleTweaks.Tooltip.AddModuleShiftCtrl"]   = "Shift+\u70b9\u51fb: \u6dfb\u52a0\u00d710\nCtrl+\u70b9\u51fb: \u6dfb\u52a0\u00d7100",
                ["SimpleTweaks.Tooltip.RemoveModuleShiftCtrl"]= "Shift+\u70b9\u51fb: \u79fb\u9664\u00d710\nCtrl+\u70b9\u51fb: \u79fb\u9664\u00d7100",
                ["SimpleTweaks.Tooltip.CancelAllBuildings"]   = "Shift+\u70b9\u51fb: \u53d6\u6d88\u8be5\u661f\u4f53\u4e0a\u6240\u6709\u6b63\u5728\u5efa\u9020\u7684\u5efa\u7b51",
            },
        };

        public static string Get(string locale, string key)
        {
            if (_translations.TryGetValue(locale, out var dict) && dict.TryGetValue(key, out var val))
                return val;
            // fallback to en-US
            if (_translations.TryGetValue("en-US", out var en) && en.TryGetValue(key, out var enVal))
                return enVal;
            return key;
        }
    }
}
