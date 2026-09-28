using UnityEngine;

namespace Mahjong.Game
{
    /// <summary>
    /// Synthetisch erzeugte Soundeffekte (PCM per Code, keine Audio-Assets):
    /// WebGL-tauglich, winzig, und klingt trotzdem nicht nach Systempiepser.
    /// </summary>
    public static class TileAudio
    {
        const int Rate = 44100;

        static AudioClip _select;
        static AudioClip _match;
        static AudioClip _win;
        static AudioClip _deadlock;

        /// <summary>Kurzer, weicher Klick beim Auswaehlen.</summary>
        public static AudioClip Select()
        {
            if (_select == null)
            {
                // Sinus 1150 Hz mit schnellem, natuerlichem Decay.
                _select = Tone("Select", 0.09f, t =>
                {
                    var env = Mathf.Exp(-t * 60f);
                    return Mathf.Sin(2f * Mathf.PI * 1150f * t) * env * 0.35f
                        + Mathf.Sin(2f * Mathf.PI * 2300f * t) * env * env * 0.08f;
                });
            }

            return _select;
        }

        /// <summary>Sattes "Klack" beim Paar-Match: zwei Toene kurz nacheinander.</summary>
        public static AudioClip Match()
        {
            if (_match == null)
            {
                _match = Tone("Match", 0.30f, t =>
                {
                    var env = Mathf.Exp(-t * 18f);
                    var pitch = t < 0.06f ? 660f : 990f;
                    return Mathf.Sin(2f * Mathf.PI * pitch * t) * env * 0.5f;
                });
            }

            return _match;
        }

        /// <summary>Kurzes Arpeggio beim Sieg.</summary>
        public static AudioClip Win()
        {
            if (_win == null)
            {
                float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };

                _win = Tone("Win", 1.0f, t =>
                {
                    var index = Mathf.Min((int)(t / 0.18f), notes.Length - 1);
                    var local = t - index * 0.18f;
                    var env = Mathf.Exp(-local * 7f) * Mathf.Exp(-t * 1.5f);
                    return Mathf.Sin(2f * Mathf.PI * notes[index] * t) * env * 0.4f;
                });
            }

            return _win;
        }

        /// <summary>Tiefer, dumpfer Ton beim Deadlock.</summary>
        public static AudioClip Deadlock()
        {
            if (_deadlock == null)
            {
                _deadlock = Tone("Deadlock", 0.6f, t =>
                {
                    var env = Mathf.Exp(-t * 6f);
                    return (Mathf.Sin(2f * Mathf.PI * 160f * t)
                        + Mathf.Sin(2f * Mathf.PI * 190f * t) * 0.7f) * env * 0.4f;
                });
            }

            return _deadlock;
        }

        static AudioClip Tone(string name, float seconds, System.Func<float, float> generator)
        {
            var samples = Mathf.CeilToInt(seconds * Rate);
            var data = new float[samples];

            for (var i = 0; i < samples; i++)
            {
                data[i] = generator(i / (float)Rate);
            }

            var clip = AudioClip.Create(name, samples, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}