using System.Collections.Generic;

namespace SimpleTweaks
{
    internal static class LocalisationData
    {
        private static readonly Dictionary<string, Dictionary<string, string>> _translations =
            new Dictionary<string, Dictionary<string, string>>
        {
            ["en-US"] = new Dictionary<string, string>
            {
                ["SimpleTweaks.Tooltip.CtrlHint"]              = "Ctrl+Click: \u00b1100",
                ["SimpleTweaks.Tooltip.BuildShiftCtrl"]        = "Shift+Click: \u00d710\nCtrl+Click: \u00d7100",
                ["SimpleTweaks.Tooltip.AddModuleShiftCtrl"]    = "Shift+Click: add \u00d710\nCtrl+Click: add \u00d7100",
                ["SimpleTweaks.Tooltip.RemoveModuleShiftCtrl"] = "Shift+Click: remove \u00d710\nCtrl+Click: remove \u00d7100",
                ["SimpleTweaks.Tooltip.CancelAllBuildings"]    = "Shift+Click: cancel all buildings of this type under construction on this body",
                ["SimpleTweaks.Tooltip.CancelAllConstruction"] = "Shift+Click: cancel all items of this type in the construction queue",
                ["SimpleTweaks.Tooltip.DeleteAsteroid"]        = "Destroy object",
                ["SimpleTweaks.Tooltip.GoToOrbit"]             = "Set destination to the orbit of the origin body",
                ["SimpleTweaks.Tooltip.GoToSurface"]           = "Set destination to the surface of the origin body",
            },
            ["de-DE"] = new Dictionary<string, string>
            {
                ["SimpleTweaks.Tooltip.CtrlHint"]              = "Strg+Klick: \u00b1100",
                ["SimpleTweaks.Tooltip.BuildShiftCtrl"]        = "Shift+Klick: \u00d710\nStrg+Klick: \u00d7100",
                ["SimpleTweaks.Tooltip.AddModuleShiftCtrl"]    = "Shift+Klick: \u00d710 hinzuf\u00fcgen\nStrg+Klick: \u00d7100 hinzuf\u00fcgen",
                ["SimpleTweaks.Tooltip.RemoveModuleShiftCtrl"] = "Shift+Klick: \u00d710 entfernen\nStrg+Klick: \u00d7100 entfernen",
                ["SimpleTweaks.Tooltip.CancelAllBuildings"]    = "Shift+Klick: alle Geb\u00e4ude dieses Typs im Bau auf diesem K\u00f6rper abbrechen",
                ["SimpleTweaks.Tooltip.CancelAllConstruction"] = "Shift+Klick: alle Elemente dieses Typs in der Bauwarteschlange abbrechen",
                ["SimpleTweaks.Tooltip.DeleteAsteroid"]        = "Objekt zerst\u00f6ren",
                ["SimpleTweaks.Tooltip.GoToOrbit"]             = "Ziel auf die Umlaufbahn des Ausgangsk\u00f6rpers setzen",
                ["SimpleTweaks.Tooltip.GoToSurface"]           = "Ziel auf die Oberfl\u00e4che des Ausgangsk\u00f6rpers setzen",
            },
            ["es-ES"] = new Dictionary<string, string>
            {
                ["SimpleTweaks.Tooltip.CtrlHint"]              = "Ctrl+Clic: \u00b1100",
                ["SimpleTweaks.Tooltip.BuildShiftCtrl"]        = "Shift+Clic: \u00d710\nCtrl+Clic: \u00d7100",
                ["SimpleTweaks.Tooltip.AddModuleShiftCtrl"]    = "Shift+Clic: a\u00f1adir \u00d710\nCtrl+Clic: a\u00f1adir \u00d7100",
                ["SimpleTweaks.Tooltip.RemoveModuleShiftCtrl"] = "Shift+Clic: quitar \u00d710\nCtrl+Clic: quitar \u00d7100",
                ["SimpleTweaks.Tooltip.CancelAllBuildings"]    = "Shift+Clic: cancelar todos los edificios de este tipo en construcci\u00f3n en este cuerpo",
                ["SimpleTweaks.Tooltip.CancelAllConstruction"] = "Shift+Clic: cancelar todos los elementos de este tipo en la cola de construcci\u00f3n",
                ["SimpleTweaks.Tooltip.DeleteAsteroid"]        = "Destruir objeto",
                ["SimpleTweaks.Tooltip.GoToOrbit"]             = "Establecer destino a la \u00f3rbita del cuerpo de origen",
                ["SimpleTweaks.Tooltip.GoToSurface"]           = "Establecer destino a la superficie del cuerpo de origen",
            },
            ["fr-FR"] = new Dictionary<string, string>
            {
                ["SimpleTweaks.Tooltip.CtrlHint"]              = "Ctrl+Clic: \u00b1100",
                ["SimpleTweaks.Tooltip.BuildShiftCtrl"]        = "Maj+Clic: \u00d710\nCtrl+Clic: \u00d7100",
                ["SimpleTweaks.Tooltip.AddModuleShiftCtrl"]    = "Maj+Clic: ajouter \u00d710\nCtrl+Clic: ajouter \u00d7100",
                ["SimpleTweaks.Tooltip.RemoveModuleShiftCtrl"] = "Maj+Clic: retirer \u00d710\nCtrl+Clic: retirer \u00d7100",
                ["SimpleTweaks.Tooltip.CancelAllBuildings"]    = "Maj+Clic: annuler tous les b\u00e2timents de ce type en construction sur ce corps",
                ["SimpleTweaks.Tooltip.CancelAllConstruction"] = "Maj+Clic: annuler tous les \u00e9l\u00e9ments de ce type dans la file de construction",
                ["SimpleTweaks.Tooltip.DeleteAsteroid"]        = "D\u00e9truire l'objet",
                ["SimpleTweaks.Tooltip.GoToOrbit"]             = "D\u00e9finir la destination sur l'orbite du corps d'origine",
                ["SimpleTweaks.Tooltip.GoToSurface"]           = "D\u00e9finir la destination sur la surface du corps d'origine",
            },
            ["it-IT"] = new Dictionary<string, string>
            {
                ["SimpleTweaks.Tooltip.CtrlHint"]              = "Ctrl+Clic: \u00b1100",
                ["SimpleTweaks.Tooltip.BuildShiftCtrl"]        = "Shift+Clic: \u00d710\nCtrl+Clic: \u00d7100",
                ["SimpleTweaks.Tooltip.AddModuleShiftCtrl"]    = "Shift+Clic: aggiungi \u00d710\nCtrl+Clic: aggiungi \u00d7100",
                ["SimpleTweaks.Tooltip.RemoveModuleShiftCtrl"] = "Shift+Clic: rimuovi \u00d710\nCtrl+Clic: rimuovi \u00d7100",
                ["SimpleTweaks.Tooltip.CancelAllBuildings"]    = "Shift+Clic: annulla tutti gli edifici di questo tipo in costruzione su questo corpo",
                ["SimpleTweaks.Tooltip.CancelAllConstruction"] = "Shift+Clic: annulla tutti gli elementi di questo tipo nella coda di costruzione",
                ["SimpleTweaks.Tooltip.DeleteAsteroid"]        = "Distruggi oggetto",
                ["SimpleTweaks.Tooltip.GoToOrbit"]             = "Imposta la destinazione sull'orbita del corpo di origine",
                ["SimpleTweaks.Tooltip.GoToSurface"]           = "Imposta la destinazione sulla superficie del corpo di origine",
            },
            ["jp-JP"] = new Dictionary<string, string>
            {
                ["SimpleTweaks.Tooltip.CtrlHint"]              = "Ctrl+\u30af\u30ea\u30c3\u30af: \u00b1100",
                ["SimpleTweaks.Tooltip.BuildShiftCtrl"]        = "Shift+\u30af\u30ea\u30c3\u30af: \u00d710\nCtrl+\u30af\u30ea\u30c3\u30af: \u00d7100",
                ["SimpleTweaks.Tooltip.AddModuleShiftCtrl"]    = "Shift+\u30af\u30ea\u30c3\u30af: \u00d710 \u8ffd\u52a0\nCtrl+\u30af\u30ea\u30c3\u30af: \u00d7100 \u8ffd\u52a0",
                ["SimpleTweaks.Tooltip.RemoveModuleShiftCtrl"] = "Shift+\u30af\u30ea\u30c3\u30af: \u00d710 \u524a\u9664\nCtrl+\u30af\u30ea\u30c3\u30af: \u00d7100 \u524a\u9664",
                ["SimpleTweaks.Tooltip.CancelAllBuildings"]    = "Shift+\u30af\u30ea\u30c3\u30af: \u3053\u306e\u661f\u4f53\u3067\u5efa\u8a2d\u4e2d\u306e\u3053\u306e\u30bf\u30a4\u30d7\u306e\u5168\u5efa\u7269\u3092\u30ad\u30e3\u30f3\u30bb\u30eb",
                ["SimpleTweaks.Tooltip.CancelAllConstruction"] = "Shift+\u30af\u30ea\u30c3\u30af: \u5efa\u8a2d\u4e88\u5b9a\u306e\u3053\u306e\u30bf\u30a4\u30d7\u3092\u3059\u3079\u3066\u30ad\u30e3\u30f3\u30bb\u30eb",
                ["SimpleTweaks.Tooltip.DeleteAsteroid"]        = "\u30aa\u30d6\u30b8\u30a7\u30af\u30c8\u3092\u7834\u58ca",
                ["SimpleTweaks.Tooltip.GoToOrbit"]             = "\u76ee\u7684\u5730\u3092\u51fa\u767a\u5929\u4f53\u306e\u8ecc\u9053\u306b\u8a2d\u5b9a",
                ["SimpleTweaks.Tooltip.GoToSurface"]           = "\u76ee\u7684\u5730\u3092\u51fa\u767a\u5929\u4f53\u306e\u5730\u8868\u306b\u8a2d\u5b9a",
            },
            ["ko-KO"] = new Dictionary<string, string>
            {
                ["SimpleTweaks.Tooltip.CtrlHint"]              = "Ctrl+\ud074\ub9ad: \u00b1100",
                ["SimpleTweaks.Tooltip.BuildShiftCtrl"]        = "Shift+\ud074\ub9ad: \u00d710\nCtrl+\ud074\ub9ad: \u00d7100",
                ["SimpleTweaks.Tooltip.AddModuleShiftCtrl"]    = "Shift+\ud074\ub9ad: \u00d710 \ucd94\uac00\nCtrl+\ud074\ub9ad: \u00d7100 \ucd94\uac00",
                ["SimpleTweaks.Tooltip.RemoveModuleShiftCtrl"] = "Shift+\ud074\ub9ad: \u00d710 \uc81c\uac70\nCtrl+\ud074\ub9ad: \u00d7100 \uc81c\uac70",
                ["SimpleTweaks.Tooltip.CancelAllBuildings"]    = "Shift+\ud074\ub9ad: \uc774 \ucc9c\uccb4\uc758 \uac74\uc124 \uc911\uc778 \uc774 \uc720\ud615\uc758 \ubaa8\ub4e0 \uac74\ubb3c \ucde8\uc18c",
                ["SimpleTweaks.Tooltip.CancelAllConstruction"] = "Shift+\ud074\ub9ad: \uac74\uc124 \ub300\uae30\uc5f4\uc758 \uc774 \uc720\ud615 \ubaa8\ub4e0 \ud56d\ubaa9 \ucde8\uc18c",
                ["SimpleTweaks.Tooltip.DeleteAsteroid"]        = "\uac1c\uccb4 \ud30c\uad34",
                ["SimpleTweaks.Tooltip.GoToOrbit"]             = "\ubaa9\uc801\uc9c0\ub97c \ucd9c\ubc1c \uccb4\uc758 \uada4\ub3c4\ub85c \uc124\uc815",
                ["SimpleTweaks.Tooltip.GoToSurface"]           = "\ubaa9\uc801\uc9c0\ub97c \ucd9c\ubc1c \uccb4\uc758 \ud45c\uba74\uc73c\ub85c \uc124\uc815",
            },
            ["pl-PL"] = new Dictionary<string, string>
            {
                ["SimpleTweaks.Tooltip.CtrlHint"]              = "Ctrl+klik: \u00b1100",
                ["SimpleTweaks.Tooltip.BuildShiftCtrl"]        = "Shift+klik: \u00d710\nCtrl+klik: \u00d7100",
                ["SimpleTweaks.Tooltip.AddModuleShiftCtrl"]    = "Shift+klik: dodaj \u00d710\nCtrl+klik: dodaj \u00d7100",
                ["SimpleTweaks.Tooltip.RemoveModuleShiftCtrl"] = "Shift+klik: usu\u0144 \u00d710\nCtrl+klik: usu\u0144 \u00d7100",
                ["SimpleTweaks.Tooltip.CancelAllBuildings"]    = "Shift+klik: anuluj wszystkie budynki tego typu w budowie na tym ciele",
                ["SimpleTweaks.Tooltip.CancelAllConstruction"] = "Shift+klik: anuluj wszystkie elementy tego typu w kolejce budowy",
                ["SimpleTweaks.Tooltip.DeleteAsteroid"]        = "Zniszcz obiekt",
                ["SimpleTweaks.Tooltip.GoToOrbit"]             = "Ustaw cel na orbit\u0119 cia\u0142a \u017ar\u00f3d\u0142owego",
                ["SimpleTweaks.Tooltip.GoToSurface"]           = "Ustaw cel na powierzchni\u0119 cia\u0142a \u017ar\u00f3d\u0142owego",
            },
            ["pt-BR"] = new Dictionary<string, string>
            {
                ["SimpleTweaks.Tooltip.CtrlHint"]              = "Ctrl+Clique: \u00b1100",
                ["SimpleTweaks.Tooltip.BuildShiftCtrl"]        = "Shift+Clique: \u00d710\nCtrl+Clique: \u00d7100",
                ["SimpleTweaks.Tooltip.AddModuleShiftCtrl"]    = "Shift+Clique: adicionar \u00d710\nCtrl+Clique: adicionar \u00d7100",
                ["SimpleTweaks.Tooltip.RemoveModuleShiftCtrl"] = "Shift+Clique: remover \u00d710\nCtrl+Clique: remover \u00d7100",
                ["SimpleTweaks.Tooltip.CancelAllBuildings"]    = "Shift+Clique: cancelar todos os edif\u00edcios deste tipo em constru\u00e7\u00e3o neste corpo",
                ["SimpleTweaks.Tooltip.CancelAllConstruction"] = "Shift+Clique: cancelar todos os itens deste tipo na fila de constru\u00e7\u00e3o",
                ["SimpleTweaks.Tooltip.DeleteAsteroid"]        = "Destruir objeto",
                ["SimpleTweaks.Tooltip.GoToOrbit"]             = "Definir destino para a \u00f3rbita do corpo de origem",
                ["SimpleTweaks.Tooltip.GoToSurface"]           = "Definir destino para a superf\u00edcie do corpo de origem",
            },
            ["ru-RU"] = new Dictionary<string, string>
            {
                ["SimpleTweaks.Tooltip.CtrlHint"]              = "Ctrl+\u041a\u043b\u0438\u043a: \u00b1100",
                ["SimpleTweaks.Tooltip.BuildShiftCtrl"]        = "Shift+\u041a\u043b\u0438\u043a: \u00d710\nCtrl+\u041a\u043b\u0438\u043a: \u00d7100",
                ["SimpleTweaks.Tooltip.AddModuleShiftCtrl"]    = "Shift+\u041a\u043b\u0438\u043a: \u0434\u043e\u0431\u0430\u0432\u0438\u0442\u044c \u00d710\nCtrl+\u041a\u043b\u0438\u043a: \u0434\u043e\u0431\u0430\u0432\u0438\u0442\u044c \u00d7100",
                ["SimpleTweaks.Tooltip.RemoveModuleShiftCtrl"] = "Shift+\u041a\u043b\u0438\u043a: \u0443\u0434\u0430\u043b\u0438\u0442\u044c \u00d710\nCtrl+\u041a\u043b\u0438\u043a: \u0443\u0434\u0430\u043b\u0438\u0442\u044c \u00d7100",
                ["SimpleTweaks.Tooltip.CancelAllBuildings"]    = "Shift+\u041a\u043b\u0438\u043a: \u043e\u0442\u043c\u0435\u043d\u0438\u0442\u044c \u0432\u0441\u0435 \u0441\u0442\u0440\u043e\u044f\u0449\u0438\u0435\u0441\u044f \u0437\u0434\u0430\u043d\u0438\u044f \u044d\u0442\u043e\u0433\u043e \u0442\u0438\u043f\u0430",
                ["SimpleTweaks.Tooltip.CancelAllConstruction"] = "Shift+\u041a\u043b\u0438\u043a: \u043e\u0442\u043c\u0435\u043d\u0438\u0442\u044c \u0432\u0441\u0435 \u044d\u043b\u0435\u043c\u0435\u043d\u0442\u044b \u044d\u0442\u043e\u0433\u043e \u0442\u0438\u043f\u0430 \u0432 \u043e\u0447\u0435\u0440\u0435\u0434\u0438 \u0441\u0442\u0440\u043e\u0438\u0442\u0435\u043b\u044c\u0441\u0442\u0432\u0430",
                ["SimpleTweaks.Tooltip.DeleteAsteroid"]        = "\u0423\u043d\u0438\u0447\u0442\u043e\u0436\u0438\u0442\u044c \u043e\u0431\u044a\u0435\u043a\u0442",
                ["SimpleTweaks.Tooltip.GoToOrbit"]             = "\u0423\u0441\u0442\u0430\u043d\u043e\u0432\u0438\u0442\u044c \u043d\u0430\u0437\u043d\u0430\u0447\u0435\u043d\u0438\u0435 \u043d\u0430 \u043e\u0440\u0431\u0438\u0442\u0443 \u043d\u0430\u0447\u0430\u043b\u044c\u043d\u043e\u0433\u043e \u0442\u0435\u043b\u0430",
                ["SimpleTweaks.Tooltip.GoToSurface"]           = "\u0423\u0441\u0442\u0430\u043d\u043e\u0432\u0438\u0442\u044c \u043d\u0430\u0437\u043d\u0430\u0447\u0435\u043d\u0438\u0435 \u043d\u0430 \u043f\u043e\u0432\u0435\u0440\u0445\u043d\u043e\u0441\u0442\u044c \u043d\u0430\u0447\u0430\u043b\u044c\u043d\u043e\u0433\u043e \u0442\u0435\u043b\u0430",
            },
            ["tr-TR"] = new Dictionary<string, string>
            {
                ["SimpleTweaks.Tooltip.CtrlHint"]              = "Ctrl+T\u0131kla: \u00b1100",
                ["SimpleTweaks.Tooltip.BuildShiftCtrl"]        = "Shift+T\u0131kla: \u00d710\nCtrl+T\u0131kla: \u00d7100",
                ["SimpleTweaks.Tooltip.AddModuleShiftCtrl"]    = "Shift+T\u0131kla: \u00d710 ekle\nCtrl+T\u0131kla: \u00d7100 ekle",
                ["SimpleTweaks.Tooltip.RemoveModuleShiftCtrl"] = "Shift+T\u0131kla: \u00d710 kald\u0131r\nCtrl+T\u0131kla: \u00d7100 kald\u0131r",
                ["SimpleTweaks.Tooltip.CancelAllBuildings"]    = "Shift+T\u0131kla: bu cisimdeki yap\u0131m a\u015famas\u0131ndaki bu tipteki t\u00fcm binalar\u0131 iptal et",
                ["SimpleTweaks.Tooltip.CancelAllConstruction"] = "Shift+T\u0131kla: in\u015faat s\u0131ras\u0131ndaki bu tipteki t\u00fcm \u00f6\u011feleri iptal et",
                ["SimpleTweaks.Tooltip.DeleteAsteroid"]        = "Nesneyi yok et",
                ["SimpleTweaks.Tooltip.GoToOrbit"]             = "Hedefi k\u00f6ken cismin y\u00fcr\u00fcngesine ayarla",
                ["SimpleTweaks.Tooltip.GoToSurface"]           = "Hedefi k\u00f6ken cismin y\u00fcze yine ayarla",
            },
            ["zh-CN"] = new Dictionary<string, string>
            {
                ["SimpleTweaks.Tooltip.CtrlHint"]              = "Ctrl+\u70b9\u51fb: \u00b1100",
                ["SimpleTweaks.Tooltip.BuildShiftCtrl"]        = "Shift+\u70b9\u51fb: \u00d710\nCtrl+\u70b9\u51fb: \u00d7100",
                ["SimpleTweaks.Tooltip.AddModuleShiftCtrl"]    = "Shift+\u70b9\u51fb: \u6dfb\u52a0\u00d710\nCtrl+\u70b9\u51fb: \u6dfb\u52a0\u00d7100",
                ["SimpleTweaks.Tooltip.RemoveModuleShiftCtrl"] = "Shift+\u70b9\u51fb: \u79fb\u9664\u00d710\nCtrl+\u70b9\u51fb: \u79fb\u9664\u00d7100",
                ["SimpleTweaks.Tooltip.CancelAllBuildings"]    = "Shift+\u70b9\u51fb: \u53d6\u6d88\u8be5\u661f\u4f53\u4e0a\u6240\u6709\u6b63\u5728\u5efa\u9020\u7684\u6b64\u7c7b\u578b\u5efa\u7b51",
                ["SimpleTweaks.Tooltip.CancelAllConstruction"] = "Shift+\u70b9\u51fb: \u53d6\u6d88\u5efa\u9020\u961f\u5217\u4e2d\u6240\u6709\u6b64\u7c7b\u578b\u9879\u76ee",
                ["SimpleTweaks.Tooltip.DeleteAsteroid"]        = "\u6467\u6bc1\u5929\u4f53",
                ["SimpleTweaks.Tooltip.GoToOrbit"]             = "\u5c06\u76ee\u7684\u5730\u8bbe\u4e3a\u8d77\u6e90\u5929\u4f53\u7684\u8ecc\u9053",
                ["SimpleTweaks.Tooltip.GoToSurface"]           = "\u5c06\u76ee\u7684\u5730\u8bbe\u4e3a\u8d77\u6e90\u5929\u4f53\u7684\u8868\u9762",
            },
        };

        public static string Get(string locale, string key)
        {
            if (_translations.TryGetValue(locale, out var dict) && dict.TryGetValue(key, out var val))
                return val;
            if (_translations.TryGetValue("en-US", out var en) && en.TryGetValue(key, out var enVal))
                return enVal;
            return key;
        }
    }
}
