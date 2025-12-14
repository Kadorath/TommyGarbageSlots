using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

using Random = UnityEngine.Random;

public class GrabbyPawBehaviour : MonoBehaviour
{
    private RectTransform rT;
    [SerializeField] Vector2 startPos;
    [SerializeField] Vector2 targetPos;

    [SerializeField] GameObject targetSymbol;

    void OnEnable()
    {
        rT = GetComponent<RectTransform>();
    }
    public void Init(Vector2 t, Vector2 s, float rot, GameObject symbol)
    {
        targetSymbol = symbol;
        transform.localRotation = Quaternion.Euler(0f, 0f, rot);
        targetPos = t;
        startPos = s;
        rT.anchoredPosition = startPos;
        StartCoroutine(Grab());
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    IEnumerator Grab()
    {
        // Base Delay
        yield return new WaitForSeconds(1f);
        // Stagger Delay
        yield return new WaitForSeconds(Random.Range(0f, 0.5f));

        for (int i = 0; i < 40; i ++)
        {
            rT.anchoredPosition = Vector2.Lerp(startPos, targetPos, CubicEaseOut(i/40f));
            yield return new WaitForFixedUpdate();
        }

        targetSymbol.GetComponent<Image>().enabled = false;

        for (int i = 0; i < 40; i ++)
        {
            rT.anchoredPosition = Vector2.Lerp(targetPos, startPos, CubicEaseOut(i/40f));
            yield return new WaitForFixedUpdate();
        }
    }

    private float CubicEaseOut(float t)
    {
        return 1 - Mathf.Pow(1 - t, 3);
    }
}
