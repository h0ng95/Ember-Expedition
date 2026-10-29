using System;
using System.Collections;
using Expedition.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Expedition.Presentation
{
    public sealed partial class ExpeditionScreen
    {
        static int StartupStages { get { return 8 + StartupBackgroundPaths.Length; } }
        const string SplashPath = "Art/Branding/ignis-splash-v1";
        static readonly string[] StartupBackgroundPaths = {
            "Art/forest-battlefield", "Art/ruined-outpost-battlefield",
            "Art/Environments/v024/furnace-battlefield",
            "Art/Environments/v029/frost-battlefield",
            "Art/Environments/v030/garden-battlefield",
            "Art/Environments/v031/palace-battlefield"
        };

        bool startupReady, startupLoading;
        int startupCompleted;
        string startupStage, startupError;
        Texture2D startupSplash;
        GUIStyle startupSubtitle;
        static readonly string[][] IgnisGlyphs = {
            new[] { "11111", "00100", "00100", "00100", "00100", "00100", "11111" },
            new[] { "01111", "10000", "10000", "10111", "10001", "10001", "01111" },
            new[] { "10001", "11001", "10101", "10101", "10011", "10001", "10001" },
            new[] { "11111", "00100", "00100", "00100", "00100", "00100", "11111" },
            new[] { "01111", "10000", "10000", "01110", "00001", "00001", "11110" }
        };

        void BeginStartup()
        {
            if (startupReady || startupLoading) return;
            CancelPointer();
            if (art != null) { art.Dispose(); art = null; }
            database = null;
            run = null;
            journal = null;
            progress = null;
            battlefield = outpost = furnace = frost = garden = palace = null;
            badSave = badProgress = savePresent = legacySave = false;
            journalDirty = progressDirty = false;
            accountStore = null; loadedAccount = null; storageBlocked = savingAccount = false;
            home = true;
            modal = Modal.None;
            error = startupError = null;
            startupCompleted = 0;
            startupStage = "화면을 준비하고 있습니다";
            startupLoading = true;
            StartCoroutine(InitializeStartup());
        }

        ResourceRequest StartResource<T>(string path) where T : UnityEngine.Object
        {
            try { return Resources.LoadAsync<T>(path); }
            catch (Exception exception)
            {
                FailStartup(exception);
                return null;
            }
        }

        bool CompleteStage(Action action)
        {
            try { action(); startupCompleted++; return true; }
            catch (Exception exception) { FailStartup(exception); return false; }
        }

        void FailStartup(Exception exception)
        {
            startupError = "원정 자료를 열 수 없습니다: " + exception.Message;
            startupLoading = false;
            Debug.LogException(exception);
        }

        IEnumerator InitializeStartup()
        {
            // This request can run while Unity's native splash is still visible.
            startupStage = "시작 그림을 읽고 있습니다";
            ResourceRequest request = StartResource<Texture2D>(SplashPath);
            if (request == null) yield break;
            yield return request;
            if (!CompleteStage(() => startupSplash = request.asset as Texture2D)) yield break;
            if (!Application.isEditor)
                while (!SplashScreen.isFinished) yield return null;

            startupStage = "동료 자료를 읽고 있습니다";
            request = StartResource<TextAsset>("Content/unit-catalog-v010");
            if (request == null) yield break;
            yield return request;
            if (!CompleteStage(() => {
                TextAsset content = request.asset as TextAsset;
                if (content == null) throw new InvalidOperationException("동료 자료를 찾을 수 없습니다.");
                database = CatalogDatabase.FromJson(content.text);
                if (database == null) throw new InvalidOperationException("동료 자료 형식을 읽을 수 없습니다.");
            })) yield break;

            var backgrounds = new Texture2D[StartupBackgroundPaths.Length];
            for (int i = 0; i < StartupBackgroundPaths.Length; i++)
            {
                startupStage = "지역 그림을 읽고 있습니다 · " + (i + 1) + "/" + StartupBackgroundPaths.Length;
                request = StartResource<Texture2D>(StartupBackgroundPaths[i]);
                if (request == null) yield break;
                yield return request;
                int index = i;
                if (!CompleteStage(() => backgrounds[index] = request.asset as Texture2D)) yield break;
            }
            battlefield = backgrounds[0]; outpost = backgrounds[1]; furnace = backgrounds[2];
            frost = backgrounds[3]; garden = backgrounds[4]; palace = backgrounds[5];

            startupStage = "원정 기록을 확인하고 있습니다";
            if (!CompleteStage(LoadJournal)) yield break;
            yield return null;
            startupStage = "성과와 연구를 확인하고 있습니다";
            if (!CompleteStage(LoadProgress)) yield break;
            yield return null;
            startupStage = "이전 원정을 확인하고 있습니다";
            if (!CompleteStage(LoadSave)) yield break;
            yield return null;
            startupStage = "안내 상태를 확인하고 있습니다";
            if (!CompleteStage(LoadTutorial)) yield break;
            if (!CompleteStage(LoadLessons)) yield break;
            yield return null;
            startupStage = "동료 그림을 준비하고 있습니다";
            if (!CompleteStage(() => art = new CatalogArtView())) yield break;

            startupStage = "준비 완료";
            startupLoading = false;
            yield return null; // Present the completed state before entering the home screen.
            float completeUntil = Time.unscaledTime + .3f;
            while (Time.unscaledTime < completeUntil) yield return null;
            startupReady = true;
            FinalizeAccountLoad();
        }

        static void DrawStartupGradient(bool vertical, float start, float length, int bands, float firstAlpha, float lastAlpha)
        {
            for (int i = 0; i < bands; i++)
            {
                float t = (i + .5f) / bands;
                float alpha = Mathf.Lerp(firstAlpha, lastAlpha, t);
                float offset = Mathf.Round(start + i * length / bands);
                float extent = Mathf.Round(start + (i + 1) * length / bands) - offset;
                Fill(vertical ? new Rect(0, offset, Width, extent) :
                    new Rect(offset, 0, extent, Height),
                    new Color(.012f, .025f, .055f, alpha));
            }
        }

        static void DrawIgnisPixels(float x, float y, float pixel)
        {
            Color shadow = new Color(.04f, .035f, .025f, .78f);
            for (int letter = 0; letter < IgnisGlyphs.Length; letter++)
                for (int row = 0; row < 7; row++)
                    for (int col = 0; col < 5; col++)
                    {
                        if (IgnisGlyphs[letter][row][col] != '1') continue;
                        float px = x + (letter * 6 + col) * pixel;
                        float py = y + row * pixel;
                        Fill(new Rect(px + 3, py + 4, pixel - 1, pixel - 1), shadow);
                        Fill(new Rect(px, py, pixel - 1, pixel - 1), Gold);
                    }
        }

        static void DrawStartupEmbers()
        {
            float time = Time.unscaledTime;
            for (int i = 0; i < 8; i++)
            {
                float phase = Mathf.Repeat(time * (.14f + i * .009f) + i * .137f, 1f);
                float x = 872f + (i * 29 % 61) + Mathf.Sin(time * .8f + i * 1.7f) * 7f;
                float y = 463f - phase * 70f;
                float alpha = Mathf.Sin(phase * Mathf.PI) * .85f;
                float size = 3f + i % 3;
                Fill(new Rect(x, y, size, size), new Color(1f, .57f + .045f * (i % 3), .19f, alpha));
            }
        }

        void DrawStartup()
        {
            GUI.enabled = true;
            if (startupSplash != null)
                GUI.DrawTexture(new Rect(0, 0, Width, Height), startupSplash, ScaleMode.ScaleAndCrop);
            else Fill(new Rect(0, 0, Width, Height), Background);
            Fill(new Rect(0, 0, Width, Height), new Color(.025f, .045f, .08f, .13f));
            DrawStartupGradient(false, 0, 750, 40, .38f, 0f);
            DrawStartupGradient(true, 530, 190, 24, .01f, .72f);
            for (int i = 0; i < 18; i++)
                Fill(new Rect(82 + i * 31, 90, 14, 2), new Color(Gold.r, Gold.g, Gold.b, .24f));
            if (startupSubtitle == null)
                startupSubtitle = Style(28, Cream, true, TextAnchor.MiddleLeft);
            DrawIgnisPixels(91, 135, 15);
            GUI.Label(new Rect(93, 246, 570, 42), "ASHEN OATH", startupSubtitle);
            GUI.Label(new Rect(94, 297, 535, 42), "잿빛 서약 · 왕국의 불을 찾아", body);
            DrawStartupEmbers();
            Rect loadingBar = new Rect(89, 615, 1088, 8);
            Fill(loadingBar, new Color(.29f, .36f, .43f, .75f));
            float completedWidth = loadingBar.width * Mathf.Clamp01((float)startupCompleted / StartupStages);
            if (completedWidth > 0f)
                Fill(new Rect(loadingBar.x, loadingBar.y, completedWidth, loadingBar.height), Gold);
            if (startupError == null)
            {
                GUI.Label(new Rect(88, 565, 1070, 40), startupStage, heading);
                GUI.Label(new Rect(88, 640, 1080, 36),
                    "완료된 준비 " + startupCompleted + " / " + StartupStages, small);
            }
            else
            {
                GUI.Label(new Rect(88, 558, 1020, 42), "시작 준비가 중단되었습니다", heading);
                GUI.Label(new Rect(88, 635, 840, 60), startupError, body);
                if (GUI.Button(new Rect(990, 642, 190, 53), "다시 시도", primary)) BeginStartup();
            }
        }
    }
}
