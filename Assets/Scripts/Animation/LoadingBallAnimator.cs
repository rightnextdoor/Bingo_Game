using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class LoadingBallAnimator : MonoBehaviour
{
    #region Inspector Fields

    [Header("Balls")]
    [SerializeField] private List<Transform> balls = new List<Transform>();

    [Header("Ball Colors")]
    [SerializeField]
    private List<Color> ballColors = new List<Color>
{
    new Color(1f, 0.25f, 0.25f),
    new Color(0.25f, 0.55f, 1f),
    new Color(1f, 0.85f, 0.2f),
    new Color(0.25f, 0.8f, 0.45f),
    new Color(0.75f, 0.4f, 1f)
};

    [Header("Camera Fit")]
    [SerializeField] private Camera loadingBallCamera;
    [SerializeField] private bool autoFitCamera = true;
    [SerializeField] private float cameraPadding = 0.25f;

    [Header("Layout")]
    [SerializeField] private float ballSpacing = 0.65f;
    [SerializeField] private float ballScale = 0.45f;

    [Header("Wave Bounce")]
    [SerializeField] private float bounceHeight = 0.18f;
    [SerializeField] private float bounceSpeed = 8.5f;

    #endregion

    #region Private Fields

    private readonly string[] ballLetters = { "B", "I", "N", "G", "O" };

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private MaterialPropertyBlock materialPropertyBlock;
    private readonly List<Vector3> ballStartPositions = new List<Vector3>();

    #endregion

    #region Unity Methods

    private void Awake()
    {
        materialPropertyBlock = new MaterialPropertyBlock();

        SetupBalls();
    }

    private void OnValidate()
    {
        SetupBalls();
    }

    private void Update()
    {
        AnimateBalls();
    }

    #endregion

    #region Setup

    private void SetupBalls()
    {
        RemoveMissingBalls();
        PositionBalls();
        ApplyBallScale();
        ApplyBallLetters();
        ApplyBallColors();
        CacheStartPositions();
        AutoFitCameraToBalls();
    }

    private void RemoveMissingBalls()
    {
        for (int i = balls.Count - 1; i >= 0; i--)
        {
            if (balls[i] != null)
            {
                continue;
            }

            balls.RemoveAt(i);
        }
    }

    private void PositionBalls()
    {
        if (balls.Count == 0)
        {
            return;
        }

        float centerOffset = (balls.Count - 1) * 0.5f;

        for (int i = 0; i < balls.Count; i++)
        {
            Transform ball = balls[i];

            if (ball == null)
            {
                continue;
            }

            float xPosition = (i - centerOffset) * ballSpacing;

            ball.localPosition = new Vector3(xPosition, 0f, 0f);
        }
    }

    private void ApplyBallScale()
    {
        for (int i = 0; i < balls.Count; i++)
        {
            Transform ball = balls[i];

            if (ball == null)
            {
                continue;
            }

            ball.localScale = Vector3.one * ballScale;
        }
    }

    private void ApplyBallLetters()
    {
        for (int i = 0; i < balls.Count; i++)
        {
            Transform ball = balls[i];

            if (ball == null)
            {
                continue;
            }

            TMP_Text ballText = ball.GetComponentInChildren<TMP_Text>(true);

            if (ballText == null)
            {
                continue;
            }

            ballText.text = GetBallLetter(i);
        }
    }

    private string GetBallLetter(int index)
    {
        if (index < 0 || index >= ballLetters.Length)
        {
            return string.Empty;
        }

        return ballLetters[index];
    }

    private void ApplyBallColors()
    {
        for (int i = 0; i < balls.Count; i++)
        {
            SetBallColor(balls[i], GetBallColor(i));
        }
    }

    private Color GetBallColor(int index)
    {
        if (ballColors == null || ballColors.Count == 0)
        {
            return Color.white;
        }

        if (index < 0 || index >= ballColors.Count)
        {
            return Color.white;
        }

        return ballColors[index];
    }

    private void SetBallColor(Transform ball, Color color)
    {
        if (ball == null)
        {
            return;
        }

        Renderer ballRenderer = ball.GetComponent<Renderer>();

        if (ballRenderer == null)
        {
            return;
        }

        if (materialPropertyBlock == null)
        {
            materialPropertyBlock = new MaterialPropertyBlock();
        }

        ballRenderer.GetPropertyBlock(materialPropertyBlock);

        materialPropertyBlock.SetColor(BaseColorId, color);
        materialPropertyBlock.SetColor(ColorId, color);

        ballRenderer.SetPropertyBlock(materialPropertyBlock);
    }

    private void CacheStartPositions()
    {
        ballStartPositions.Clear();

        for (int i = 0; i < balls.Count; i++)
        {
            if (balls[i] == null)
            {
                ballStartPositions.Add(Vector3.zero);
                continue;
            }

            ballStartPositions.Add(balls[i].localPosition);
        }
    }

    private void AutoFitCameraToBalls()
    {
        if (!autoFitCamera || loadingBallCamera == null || balls.Count == 0)
        {
            return;
        }

        loadingBallCamera.orthographic = true;

        float aspect = loadingBallCamera.aspect;

        if (loadingBallCamera.targetTexture != null && loadingBallCamera.targetTexture.height > 0)
        {
            aspect = (float)loadingBallCamera.targetTexture.width / loadingBallCamera.targetTexture.height;
        }

        float rowWidth = Mathf.Max(0f, (balls.Count - 1) * ballSpacing) + ballScale;
        float halfRowWidth = (rowWidth * 0.5f) + cameraPadding;

        float halfRowHeight = (ballScale * 0.5f) + bounceHeight + cameraPadding;

        float sizeForWidth = aspect <= 0f ? halfRowHeight : halfRowWidth / aspect;
        float sizeForHeight = halfRowHeight;

        loadingBallCamera.orthographicSize = Mathf.Max(sizeForWidth, sizeForHeight);
    }

    #endregion

    #region Animation

    private void AnimateBalls()
    {
        for (int i = 0; i < balls.Count; i++)
        {
            if (i >= ballStartPositions.Count)
            {
                continue;
            }

            AnimateBall(balls[i], ballStartPositions[i], GetWaveOffset(i));
        }
    }

    private void AnimateBall(Transform ball, Vector3 startPosition, float waveOffset)
    {
        if (ball == null)
        {
            return;
        }

        float wave = Mathf.Sin((Time.unscaledTime * bounceSpeed) + waveOffset);
        float yOffset = wave * bounceHeight;

        ball.localPosition = startPosition + new Vector3(0f, yOffset, 0f);
    }

    private float GetWaveOffset(int index)
    {
        if (balls.Count <= 0)
        {
            return 0f;
        }

        return (Mathf.PI * 2f / balls.Count) * index;
    }

    #endregion
}