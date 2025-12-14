using UnityEngine;

public class SymbolIconController : MonoBehaviour
{
    [SerializeField] private bool highlighted = false;
    private Vector3 defaultScale;
    private Vector3 highlightedScale;
    private float t = 1f;
    [SerializeField] float highlightSpeed = 1f;

    void OnEnable()
    {
        highlighted = false;
        t = 1f;
        defaultScale = transform.localScale;
        highlightedScale = new Vector3(defaultScale.x+1f,defaultScale.y+1f,defaultScale.z+1f);
    }

    void Update()
    {
        t += Time.deltaTime * highlightSpeed;
        if (highlighted)
        {
            transform.localScale = Vector3.LerpUnclamped(defaultScale, highlightedScale, ElasticEaseOut(t));
        }
        else
        {
            transform.localScale = Vector3.Lerp(highlightedScale, defaultScale, t*4f);
        }
    }

    public void Highlight()
    {
        highlighted = true;
        t = 0f;
    }

    public void Reset()
    {
        if (!highlighted) return;

        highlighted = false;
        t = 0f;
    }

    private float ElasticEaseOut(float t)
    {
        if (t <= 0f) return 0f;
        if (t >= 1f) return 1f;

        float p = 0.3f; // period of oscillation
        return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t - p / 4f) * (2f * Mathf.PI) / p) + 1f;
    }
}
