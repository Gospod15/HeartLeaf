using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class CameraController : MonoBehaviour
{
    public Transform target;
    public float smoothSpeed = 5f;

    [Header("Налаштування Зуму")]
    private float zoomStep = 1f;
    private float minZoom = 3f;
    private float maxZoom = 10f;
    private float zoomDampening = 6f;

    private Camera cam;
    private float targetZoom;
    private float fixedZ = -10f;

    [Header("Налаштування Прозорості (X-Ray)")]
    public LayerMask treeLayer;
    public LayerMask bushesLayer; // Шар для кущів
    
    public float transparency = 0.5f;
    public float checkRadius = 1.5f;
    public float yOffsetCheck = 0.5f;

    // Списки для зберігання об'єктів, які зараз прозорі
    private List<SpriteRenderer> obscuredTrees = new List<SpriteRenderer>();
    private List<SpriteRenderer> obscuredBushes = new List<SpriteRenderer>();

    void Start()
    {
        cam = GetComponent<Camera>();
        targetZoom = cam.orthographicSize;
    }

    private void LateUpdate()
    {
        HandleZoom();
        HandleMovement();
        HandleTransparency();
    }

    void HandleTransparency()
    {
        if (target == null) return;

        // Викликаємо метод обробки для дерев і оновлюємо список дерев
        obscuredTrees = UpdateTransparencyForLayer(treeLayer, obscuredTrees);

        // Викликаємо той самий метод для кущів і оновлюємо список кущів
        obscuredBushes = UpdateTransparencyForLayer(bushesLayer, obscuredBushes);
    }

    // Універсальний метод для будь-якого шару (дерева або кущі)
    // Приймає шар для перевірки та список попередніх прозорих об'єктів
    // Повертає оновлений список прозорих об'єктів
    List<SpriteRenderer> UpdateTransparencyForLayer(LayerMask layer, List<SpriteRenderer> currentObscuredList)
    {
        // 1. Шукаємо колайдери на потрібному шарі
        Collider2D[] hits = Physics2D.OverlapCircleAll(target.position + Vector3.up * yOffsetCheck, checkRadius, layer);
        List<SpriteRenderer> newHits = new List<SpriteRenderer>();

        // 2. Перевіряємо нові перекриття
        foreach (var hit in hits)
        {
            SpriteRenderer sr = hit.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                // Перевірка: чи гравець вище (по Y) за об'єкт (тобто "за" ним)
                if (target.position.y > hit.transform.position.y)
                {
                    Color color = sr.color;
                    color.a = transparency; // Робимо прозорим
                    sr.color = color;

                    newHits.Add(sr);
                }
            }
        }

        // 3. Відновлюємо прозорість для тих, хто більше не перекриває гравця
        foreach (var oldSr in currentObscuredList)
        {
            // Якщо старого об'єкта немає в новому списку перекриттів -> відновлюємо його
            if (!newHits.Contains(oldSr) && oldSr != null)
            {
                Color color = oldSr.color;
                color.a = 1f; // Повертаємо непрозорість
                oldSr.color = color;
            }
        }

        return newHits;
    }

    void HandleZoom()
    {
        float scrollInput = 0f;
        if (Mouse.current != null)
        {
            float rawScroll = Mouse.current.scroll.ReadValue().y;
            if (rawScroll > 0) scrollInput = 1f;
            else if (rawScroll < 0) scrollInput = -1f;
        }

        if (scrollInput != 0f)
        {
            targetZoom -= scrollInput * zoomStep;
            targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
        }

        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetZoom, Time.deltaTime * zoomDampening);
    }

    void HandleMovement()
    {
        if (target == null) return;

        Vector3 desiredPosition = new Vector3(
            target.position.x,
            target.position.y + 0.50f,
            fixedZ
        );

        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        transform.position = new Vector3(transform.position.x, transform.position.y, fixedZ);
    }
}