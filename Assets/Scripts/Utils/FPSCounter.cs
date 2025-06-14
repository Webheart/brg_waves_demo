using System.Globalization;
using TMPro;
using UnityEngine;

namespace Utils
{
    public class FPSCounter : MonoBehaviour
    {
        [SerializeField] TMP_Text text;
        [SerializeField] int frameRange = 60;
        [SerializeField] int targetFrameRate = 60;

        int[] fpsBuffer;
        int fpsBufferIndex;

        void Awake()
        {
            InitializeBuffer();
            Application.targetFrameRate = targetFrameRate;
        }

        void Update()
        {
            UpdateBuffer();
            CalculateFPS();
        }

        void InitializeBuffer()
        {
            if (frameRange <= 0)
            {
                frameRange = 1;
            }

            fpsBuffer = new int[frameRange];
            fpsBufferIndex = 0;
        }

        void UpdateBuffer()
        {
            fpsBuffer[fpsBufferIndex++] = (int)(1f / Time.unscaledDeltaTime);
            if (fpsBufferIndex >= frameRange)
            {
                fpsBufferIndex = 0;
            }
        }

        void CalculateFPS()
        {
            int sum = 0;
            for (int i = 0; i < frameRange; i++)
            {
                int fps = fpsBuffer[i];
                sum += fps;
            }

            float average = Mathf.RoundToInt(sum / (float)frameRange);

            text.text = $"{average.ToString(CultureInfo.InvariantCulture)} FPS";
        }
    }
}