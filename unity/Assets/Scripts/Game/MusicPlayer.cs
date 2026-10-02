using System.Collections.Generic;
using UnityEngine;

namespace Mahjong.Game
{
    /// <summary>
    /// Hintergrundmusik: Playlist aus Resources/Music, zufaellig gemischt
    /// (Fisher-Yates), spielt Track fuer Track und mischt neu, wenn alle
    /// einmal liefen. Startet NICHT sofort: Browser sperren Audio ohne
    /// Nutzerinteraktion (WebGL-Autoplay-Sperre) — der Start passiert auf den
    /// ersten Eingabeklick, und Unity resumed den AudioContext mit. Der
    /// Menue-Schalter (An/Aus) pausiert fortlaufend statt neu zu starten.
    /// </summary>
    public class MusicPlayer : MonoBehaviour
    {
        const string MusicPref = "Mahjong.Music";
        const float Volume = 0.55f;

        // Resource-Pfade (ohne "Music/") in fester Grundreihenfolge vor dem Mischen.
        static readonly string[] Tracks =
        {
            "asianoriental2",
            "fools_philosophy",
            "china_town",
            "mahjongg_nes_bgm",
            "mixkit_1065",
        };

        static MusicPlayer _instance;

        AudioSource _audio;
        readonly List<AudioClip> _queue = new List<AudioClip>();
        bool _on;
        bool _started;

        public static MusicPlayer Ensure()
        {
            if (_instance == null)
            {
                var go = new GameObject("MusicPlayer");
                Object.DontDestroyOnLoad(go); // bleibt auch ueber ClearBoard hinweg
                _instance = go.AddComponent<MusicPlayer>();
            }

            return _instance;
        }

        void Awake()
        {
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.loop = false;
            _audio.playOnAwake = false;
            _audio.volume = Volume;
            _on = PlayerPrefs.GetInt(MusicPref, 1) == 1;
        }

        public bool On => _on;

        public void StartOnFirstInput()
        {
            // Nur tatsaechlich starten, wenn Musik an ist und noch nicht laeuft
            // (der erste Click startet; spaetere Inputs ignorieren wir).
            if (!_on || _started)
            {
                return;
            }

            _started = true;
            PlayNext();
        }

        public void Toggle()
        {
            _on = !_on;
            PlayerPrefs.SetInt(MusicPref, _on ? 1 : 0);
            PlayerPrefs.Save();

            if (!_on)
            {
                // Pausieren statt Stoppen: Wiedereinschalten laeuft weiter statt neu.
                if (_audio != null)
                {
                    _audio.Pause();
                }
                return;
            }

            if (!_started)
            {
                _started = true;
                PlayNext();
            }
            else
            {
                _audio.UnPause();
            }

            Debug.Log("[Mahjong] Musik " + (_on ? "an" : "aus"));
        }

        void Update()
        {
            // Erster Input entriegelt WebGL-Audio; danach wartet der Timer auf
            // das Track-Ende (nahtlos zum naechsten Track weiterspielen).
            if (_on && !_started
                && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)
                    || Input.touchCount > 0))
            {
                StartOnFirstInput();
                return;
            }

            // Track-Ende: weiterspielen, wenn schon EIN Track lief (clip != null).
            // Ohne den Guard wuerde ein fehlender erster Track PlayNext jede
            // Frame neu ausloesen (Endlos-Resources.Load).
            if (_started && _on && _audio.clip != null && !_audio.isPlaying)
            {
                PlayNext();
            }
        }

        /// <summary>Naechsten Track der gemischten Queue spielen; leer -> neu mischen.</summary>
        void PlayNext()
        {
            if (_queue.Count == 0)
            {
                _queue.AddRange(LoadClips());
                Shuffle(_queue);
            }

            while (_queue.Count > 0)
            {
                // Nur wirklich geladene Tracks spielen (fehlende Resources ueberspringen).
                var clip = _queue[0];
                _queue.RemoveAt(0);
                if (clip != null)
                {
                    _audio.clip = clip;
                    _audio.Play();
                    return;
                }
            }

            // Queue leer (oder alle Tracks unladbar): Quelle loesen, damit
            // Update nicht jede Frame erneut anlaeuft.
            _audio.clip = null;
        }

        static List<AudioClip> LoadClips()
        {
            var clips = new List<AudioClip>(Tracks.Length);
            foreach (var name in Tracks)
            {
                clips.Add(Resources.Load<AudioClip>("Music/" + name));
            }

            return clips;
        }

        static void Shuffle(List<AudioClip> items)
        {
            for (var i = items.Count - 1; i > 0; i--)
            {
                var j = Random.Range(0, i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }
    }
}