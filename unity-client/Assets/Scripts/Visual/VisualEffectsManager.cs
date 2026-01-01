using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TheIsland.Visual
{
    /// <summary>
    /// Manages environmental visual effects like fireflies, stars, and weather (Phase 19 & 20).
    /// </summary>
    public class VisualEffectsManager : MonoBehaviour
    {
        public static VisualEffectsManager Instance { get; private set; }

        [Header("Fireflies")]
        [SerializeField] private int fireflyCount = 20;
        [SerializeField] private Color fireflyColor = new Color(1f, 1f, 0.5f, 0.8f);
        [SerializeField] private Vector2 islandBounds = new Vector2(10, 10);

        [Header("Stars & Meteors")]
        [SerializeField] private int starCount = 50;
        [SerializeField] private Color starColor = Color.white;
        [SerializeField] private float meteorInterval = 15f;

        [Header("Weather VFX")]
        [SerializeField] private int rainDensity = 120;
        private List<GameObject> _rainDrops = new List<GameObject>();
        private string _activeWeather = "Sunny";
        private Sprite _rainSprite;

        private List<GameObject> _fireflies = new List<GameObject>();
        private List<GameObject> _stars = new List<GameObject>();
        private float _meteorTimer;

        private void Awake()
        {
            Instance = this;
            _rainSprite = CreateRainSprite();
            CreateFireflies();
            CreateStars();
            CreateRain();
        }

        public void SetWeather(string weather)
        {
            _activeWeather = weather;
            Debug.Log($"[VFXManager] Setting weather visuals for: {weather}");
            
            // Toggle systems
            bool isRainy = weather == "Rainy" || weather == "Stormy";
            foreach (var drop in _rainDrops) drop.SetActive(isRainy);
            
            if (weather == "Stormy") StartCoroutine(LightningLoop());
        }

        private void Update()
        {
            UpdateAtmosphere();
            UpdateRain();
        }

        private void UpdateAtmosphere()
        {
            float t = (Time.time % 120f) / 120f;
            bool isNight = t > 0.65f || t < 0.15f;
            float starAlpha = isNight ? (t > 0.75f || t < 0.05f ? 1f : 0.5f) : 0f;

            foreach (var star in _stars)
            {
                var sprite = star.GetComponent<SpriteRenderer>();
                sprite.color = new Color(starColor.r, starColor.g, starColor.b, starAlpha * 0.8f);
            }

            if (isNight)
            {
                _meteorTimer += Time.deltaTime;
                if (_meteorTimer >= meteorInterval)
                {
                    _meteorTimer = 0;
                    if (Random.value < 0.3f) StartCoroutine(ShootMeteor());
                }
            }
        }

        private void CreateFireflies()
        {
            for (int i = 0; i < fireflyCount; i++)
            {
                var firefly = new GameObject($"Firefly_{i}");
                firefly.transform.SetParent(transform);
                float x = Random.Range(-islandBounds.x, islandBounds.x);
                float z = Random.Range(-islandBounds.y, islandBounds.y);
                firefly.transform.position = new Vector3(x, Random.Range(1f, 3f), z);

                var sprite = firefly.AddComponent<SpriteRenderer>();
                sprite.sprite = CreateGlowSprite();
                sprite.color = fireflyColor;
                sprite.sortingOrder = 50;
                firefly.AddComponent<Billboard>();
                _fireflies.Add(firefly);
                StartCoroutine(AnimateFirefly(firefly));
            }
        }

        private void CreateStars()
        {
            for (int i = 0; i < starCount; i++)
            {
                var star = new GameObject($"Star_{i}");
                star.transform.SetParent(transform);
                float x = Random.Range(-25f, 25f);
                float y = Random.Range(10f, 15f);
                float z = Random.Range(-25f, 25f);
                star.transform.position = new Vector3(x, y, z);

                var sprite = star.AddComponent<SpriteRenderer>();
                sprite.sprite = CreateGlowSprite();
                sprite.color = new Color(1, 1, 1, 0);
                sprite.sortingOrder = -50;
                star.AddComponent<Billboard>().ConfigureForUI();
                _stars.Add(star);
            }
        }

        private void CreateRain()
        {
            for (int i = 0; i < rainDensity; i++)
            {
                var drop = new GameObject($"Rain_{i}");
                drop.transform.SetParent(transform);
                var sprite = drop.AddComponent<SpriteRenderer>();
                sprite.sprite = _rainSprite;
                sprite.color = new Color(0.8f, 0.9f, 1f, 0.6f);
                sprite.sortingOrder = 100;
                ResetRainDrop(drop);
                drop.SetActive(false);
                _rainDrops.Add(drop);
            }
        }

        private void ResetRainDrop(GameObject drop)
        {
            float x = Random.Range(-20f, 20f);
            float y = Random.Range(10f, 15f);
            float z = Random.Range(-20f, 20f);
            drop.transform.position = new Vector3(x, y, z);
        }

        private void UpdateRain()
        {
            if (_activeWeather != "Rainy" && _activeWeather != "Stormy") return;
            float speed = _activeWeather == "Stormy" ? 40f : 20f;
            foreach (var drop in _rainDrops)
            {
                drop.transform.position += Vector3.down * speed * Time.deltaTime;
                drop.transform.position += Vector3.left * speed * 0.2f * Time.deltaTime;
                if (drop.transform.position.y < -2f) ResetRainDrop(drop);
            }
        }

        private IEnumerator LightningLoop()
        {
            while (_activeWeather == "Stormy")
            {
                yield return new WaitForSeconds(Random.Range(3f, 10f));
                var flash = new GameObject("LightningFlash");
                var img = flash.AddComponent<SpriteRenderer>();
                img.sprite = CreateGlowSprite();
                img.color = new Color(1, 1, 1, 0.8f);
                flash.transform.position = new Vector3(0, 10, 0);
                flash.transform.localScale = new Vector3(100, 100, 1);
                yield return new WaitForSeconds(0.05f);
                img.color = new Color(1, 1, 1, 0.3f);
                yield return new WaitForSeconds(0.05f);
                Destroy(flash);
            }
        }

        private IEnumerator ShootMeteor()
        {
            var meteor = new GameObject("Meteor");
            meteor.transform.SetParent(transform);
            Vector3 startPos = new Vector3(Random.Range(-20, 20), 15, Random.Range(-20, 20));
            Vector3 direction = new Vector3(Random.Range(-10, 10), -5, Random.Range(-10, 10)).normalized;
            meteor.transform.position = startPos;
            var sprite = meteor.AddComponent<SpriteRenderer>();
            sprite.sprite = CreateGlowSprite();
            sprite.color = Color.white;
            sprite.transform.localScale = new Vector3(0.5f, 0.1f, 1f);
            float duration = 1.0f;
            float elapsed = 0;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                meteor.transform.position += direction * 30f * Time.deltaTime;
                sprite.color = new Color(1, 1, 1, 1f - (elapsed / duration));
                yield return null;
            }
            Destroy(meteor);
        }

        private Sprite CreateRainSprite()
        {
            Texture2D tex = new Texture2D(2, 8);
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 2; x++) tex.SetPixel(x, y, Color.white);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 2, 8), new Vector2(0.5f, 0.5f));
        }

        private Sprite CreateGlowSprite()
        {
            int size = 32;
            Texture2D tex = new Texture2D(size, size);
            tex.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++) {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(size/2f, size/2f)) / (size/2f);
                    float alpha = Mathf.Exp(-dist * 4f);
                    tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private IEnumerator AnimateFirefly(GameObject firefly)
        {
            Vector3 startPos = firefly.transform.position;
            float seed = Random.value * 100f;
            while (true)
            {
                float t = Time.time + seed;
                float dx = (Mathf.PerlinNoise(t * 0.2f, 0) - 0.5f) * 2f;
                float dy = (Mathf.PerlinNoise(0, t * 0.2f) - 0.5f) * 1f;
                float dz = (Mathf.PerlinNoise(t * 0.1f, t * 0.1f) - 0.5f) * 2f;
                firefly.transform.position = startPos + new Vector3(dx, dy, dz);
                var sprite = firefly.GetComponent<SpriteRenderer>();
                float pulse = Mathf.PingPong(t, 1f) * 0.5f + 0.5f;
                sprite.color = new Color(fireflyColor.r, fireflyColor.g, fireflyColor.b, pulse * fireflyColor.a);
                yield return null;
            }
        }
    }
}
