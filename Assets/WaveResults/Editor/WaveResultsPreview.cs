#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using ParadiseTycoon.WaveResults;
using ParadiseTycoon.Guests;
using ParadiseTycoon.Buildings;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace ParadiseTycoon.WaveResults.Editor
{
    [InitializeOnLoad]
    public static class WaveResultsPreviewMenu
    {
        const string Pending = "ParadiseTycoon.WaveResults.Preview";
        static WaveResultsPreviewMenu()
        {
            EditorApplication.playModeStateChanged += state => {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
                SessionState.SetBool(Pending, false);
                Open();
            };
        }

        [MenuItem("Tools/Paradise Tycoon/Wave results/Preview")]
        public static void Open()
        {
            if (!EditorApplication.isPlaying)
            {
                SessionState.SetBool(Pending, true);
                EditorApplication.EnterPlaymode();
                return;
            }
            if (UnityEngine.Object.FindAnyObjectByType<WaveResultsPreview>() == null)
                new GameObject("WaveResultsPreview").AddComponent<WaveResultsPreview>();
        }
    }

}
#endif
