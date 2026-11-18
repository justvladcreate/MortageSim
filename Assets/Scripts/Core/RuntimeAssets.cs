using System;
using System.Collections.Generic;
using UnityEngine;

namespace MortgageSim.Core
{
    /// <summary>Хелперы для создания рантайм-геометрии и материалов.</summary>
    public static class RuntimeAssets
    {
        private static Material _wall;
        private static Material _ceiling;
        private static Material _trim;
        private static Material _screen;
        private static Material _metal;
        private static Material _wood;
        private static Material _suit;
        private static Font _terminalFont;

        public static Material Wall
        {
            get
            {
                if (_wall == null) _wall = Lit("Wall", new Color(0.66f, 0.63f, 0.57f), 0.08f);
                return _wall;
            }
        }

        public static Material Ceiling
        {
            get
            {
                if (_ceiling == null) _ceiling = Lit("Ceiling", new Color(0.84f, 0.82f, 0.77f), 0.05f);
                return _ceiling;
            }
        }

        public static Material Trim
        {
            get
            {
                if (_trim == null) _trim = Lit("Trim", new Color(0.24f, 0.21f, 0.18f), 0.15f);
                return _trim;
            }
        }

        public static Material Screen
        {
            get
            {
                if (_screen == null)
                    _screen = Lit("Screen", new Color(0.015f, 0.025f, 0.02f), 0.35f, 0f, new Color(0.03f, 0.22f, 0.08f));
                return _screen;
            }
        }

        public static Material Metal
        {
            get
            {
                if (_metal == null) _metal = Lit("Metal", new Color(0.16f, 0.18f, 0.21f), 0.55f, 0.65f);
                return _metal;
            }
        }

        public static Material Wood
        {
            get
            {
                if (_wood == null) _wood = Lit("Wood", new Color(0.34f, 0.23f, 0.15f), 0.18f);
                return _wood;
            }
        }

        public static Material Suit
        {
            get
            {
                if (_suit == null) _suit = Lit("Suit", new Color(0.08f, 0.11f, 0.16f), 0.25f);
                return _suit;
            }
        }

        public static Font TerminalFont
        {
            get
            {
                if (_terminalFont == null)
                {
                    _terminalFont = Font.CreateDynamicFontFromOSFont(
                        new[] { "Consolas", "Cascadia Mono", "Lucida Console", "Courier New" }, 32);
                    if (_terminalFont == null)
                        _terminalFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                return _terminalFont;
            }
        }

        public static Material Lit(string name, Color color, float smoothness = 0.18f, float metallic = 0f, Color? emission = null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var mat = new Material(shader) { name = name };
            mat.SetColor("_BaseColor", color);
            mat.SetColor("_Color", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);
            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            return mat;
        }

        public static GameObject Primitive(
            PrimitiveType type,
            string name,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Material material,
            bool keepCollider = false,
            Quaternion? rotation = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation ?? Quaternion.identity;
            go.transform.localScale = scale;

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null && material != null) renderer.sharedMaterial = material;

            if (!keepCollider)
            {
                var collider = go.GetComponent<Collider>();
                if (collider != null) UnityEngine.Object.Destroy(collider);
            }

            return go;
        }

        public static Bounds GetBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            bool has = false;
            Bounds bounds = new Bounds(root.transform.position, Vector3.one * 0.1f);
            foreach (var renderer in renderers)
            {
                if (renderer is ParticleSystemRenderer) continue;
                if (!has)
                {
                    bounds = renderer.bounds;
                    has = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return bounds;
        }

        public static BoxCollider AddBoxCollider(GameObject root, Bounds worldBounds, bool isTrigger = false)
        {
            var existing = root.GetComponent<BoxCollider>();
            if (existing != null) return existing;

            var box = root.AddComponent<BoxCollider>();
            Vector3 lossy = root.transform.lossyScale;
            Vector3 localCenter = root.transform.InverseTransformPoint(worldBounds.center);
            box.center = localCenter;
            box.size = new Vector3(
                Divide(worldBounds.size.x, lossy.x),
                Divide(worldBounds.size.y, lossy.y),
                Divide(worldBounds.size.z, lossy.z));
            box.isTrigger = isTrigger;
            return box;
        }

        private static float Divide(float value, float divisor)
        {
            return Mathf.Abs(divisor) < 0.0001f ? value : value / divisor;
        }

        public static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursively(child.gameObject, layer);
        }

        public static GameObject Find(string name)
        {
            return GameObject.Find(name);
        }

        public static List<Renderer> GetRenderers(GameObject root)
        {
            var list = new List<Renderer>();
            foreach (var renderer in root.GetComponentsInChildren<Renderer>()) list.Add(renderer);
            return list;
        }
    }
}
