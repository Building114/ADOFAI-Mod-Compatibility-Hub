using Overlayer.Core;
using Overlayer.Core.Patches;
using Overlayer.Core.Scripting;
using Overlayer.Unity;
using Overlayer.Utils;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Overlayer.Patches;

public static class FontPatch {
    [LazyPatch("Patches.FontPatch.FontChanger", "RDString", "GetFontDataForLanguage")]
    public static class FontChanger {
        public static bool Prefix(ref FontData __result) {
            if(!FontManager.Initialized) {
                return true;
            }

            if(!Main.Settings.ChangeFont) {
                return true;
            }

            if(!FontManager.TryGetFont(Main.Settings.AdofaiFont.name, out var font)) {
                return true;
            }

            if(!(font.font?.dynamic ?? false)) {
                return true;
            }

            __result = font;
            return false;
        }
    }
    [LazyPatch("Patches.FontPatch.FontAttacher", "scrController", "Update")]
    public static class FontAttacher {
                                       
        static bool updating;
        static float nextUpdateTime;
        public static void Postfix(scrController __instance) {
            if(!Main.Settings.ChangeFont || updating || Time.unscaledTime < nextUpdateTime) {
                return;
            }

                                                                  
            StaticCoroutine.Run(UpdateFontCo());
        }
        static IEnumerator UpdateFontCo() {
            if(!Main.Settings.ChangeFont) {
                yield break;
            }

            updating = true;
            try {
                                                              
                                                   
                bool resolved = FontManager.TryGetFont(Main.Settings.AdofaiFont.name, out var font);
                bool fontUsable = resolved && font.font != null && font.font.dynamic;
                bool tmpUsable = resolved && font.fontTMP != null;

                List<GameObject> list = [];
                try { Main.ActiveScene.GetRootGameObjects(list); } catch { yield break; }
                foreach(var i in list) {
                    if(!i.activeSelf) {
                        continue;
                    }

                    if(i.GetComponentInChildren(typeof(scrEnableIfBeta))) {
                        continue;
                    }

                    foreach(var j in i.GetComponentsInChildren<Text>(false)) {
                        j.SetLocalizedFont();
                    }

                                                              
                                                                  
                                                       
                                                            
                    if(fontUsable) {
                        foreach(var m in i.GetComponentsInChildren<TextMesh>(false)) {
                            if(m.font == font.font) {
                                continue;
                            }

                            m.font = font.font;
                                                                
                            var renderer = m.GetComponent<MeshRenderer>();
                            if(renderer) {
                                renderer.material = font.font.material;
                            }
                        }
                    }

                    if(tmpUsable) {
                        foreach(var tmp in i.GetComponentsInChildren<TMP_Text>(false)) {
                            if(tmp.font != font.fontTMP) {
                                tmp.font = font.fontTMP;
                            }
                        }
                    }

                    yield return null;
                }

                                                
                                                         
                if(fontUsable || tmpUsable) {
                    foreach(var hitTexts in GetAllCachedHitTexts()) {
                        foreach(var pair in hitTexts) {
                            if(pair.Value == null) {
                                continue;
                            }

                            foreach(var hitText in pair.Value) {
                                ApplyFontToHitText(hitText, font, fontUsable, tmpUsable);
                            }
                        }
                    }
                }
            } finally {
                updating = false;
                nextUpdateTime = Time.unscaledTime + 0.5f;
            }
        }
                                                                            
                                                             
        static IEnumerable<Dictionary<HitMargin, scrHitTextMesh[]>> GetAllCachedHitTexts() {
            HashSet<object> seen = [];

            if(VersionSafe.FirstMemberValue(Tags.ADOFAI.Controller, "cachedHitTexts", "hitTexts")
                is Dictionary<HitMargin, scrHitTextMesh[]> fromController && seen.Add(fromController)) {
                yield return fromController;
            }

            for(int i = 0; i < VersionSafe.GetMaxPlayerCount(); i++) {
                object player = VersionSafe.GetPlayerByIndex(i);
                if(player == null) {
                    break;
                }

                if(VersionSafe.FirstMemberValue(player, "cachedHitTexts", "hitTexts")
                    is Dictionary<HitMargin, scrHitTextMesh[]> fromPlayer && seen.Add(fromPlayer)) {
                    yield return fromPlayer;
                }
            }

            if(VersionSafe.FirstMemberValue(VersionSafe.GetPlayerOne(Tags.ADOFAI.Controller), "cachedHitTexts", "hitTexts")
                is Dictionary<HitMargin, scrHitTextMesh[]> fromPlayerOne && seen.Add(fromPlayerOne)) {
                yield return fromPlayerOne;
            }
        }

        static TMP_Text FindHitTextTMP(scrHitTextMesh hitText) {
            if(hitText == null) {
                return null;
            }

            foreach(var field in hitText.GetType().GetFields(
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Instance |
                BindingFlags.Static |
                BindingFlags.FlattenHierarchy)) {
                if(typeof(TMP_Text).IsAssignableFrom(field.FieldType) && field.GetValue(hitText) is TMP_Text fieldText) {
                    return fieldText;
                }
            }

            return hitText.GetComponentInChildren<TMP_Text>(true);
        }

        static void ApplyFontToHitText(scrHitTextMesh hitText, FontData font, bool fontUsable, bool tmpUsable) {
            if(hitText == null) {
                return;
            }

            if(fontUsable) {
                foreach(var mesh in hitText.GetComponentsInChildren<TextMesh>(true)) {
                    if(mesh.font == font.font) {
                        continue;
                    }

                    mesh.font = font.font;
                    var renderer = mesh.GetComponent<MeshRenderer>();
                    if(renderer) {
                        renderer.material = font.font.material;
                    }
                }
            }

            if(tmpUsable) {
                var tmp = FindHitTextTMP(hitText);
                if(tmp != null && tmp.font != font.fontTMP) {
                    tmp.font = font.fontTMP;
                }
            }
        }
    }
}
