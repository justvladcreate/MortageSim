using System.Collections.Generic;
using DG.Tweening;
using MortgageSim.Core;
using MortgageSim.Interaction;
using MortgageSim.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace MortgageSim.World
{
    /// <summary>Собирает квартиру вокруг существующей мебели в SampleScene.</summary>
    public sealed class ApartmentBuilder : MonoBehaviour
    {
        private const float RoomLeft = -7.5f;
        private const float RoomRight = 7.5f;
        private const float RoomFront = 4.0f;
        private const float RoomBack = -10.12f;
        private const float RoomHeight = 3.3f;

        private Material _frameMaterial;
        private Material _lampMaterial;

        public void Build(GameManager game)
        {
            _frameMaterial = RuntimeAssets.Lit("DoorFrame", new Color(0.19f, 0.16f, 0.13f), 0.22f);
            _lampMaterial = RuntimeAssets.Lit("LampGlow", new Color(0.95f, 0.78f, 0.52f), 0.3f, 0f, new Color(1.8f, 1.2f, 0.55f));

            var door = RuntimeAssets.Find("pictureframe_large_A");
            Bounds doorBounds = door != null ? RuntimeAssets.GetBounds(door) : new Bounds(new Vector3(-3.32f, 1.5f, -10f), new Vector3(1.4f, 2.4f, 0.1f));

            BuildRoom(doorBounds);
            BuildLighting();
            CreateFloorCollider();

            var cabinet = RuntimeAssets.Find("cabinet_medium");
            Bounds cabinetBounds = cabinet != null
                ? RuntimeAssets.GetBounds(cabinet)
                : new Bounds(new Vector3(4.38f, 0.5f, -9.03f), new Vector3(1.3f, 1f, 0.6f));

            MonitorTerminal monitor = MonitorTerminal.Create(cabinetBounds);
            game.Terminal = monitor;

            AddComputer(cabinet, cabinetBounds, monitor);
            BuildSellables(game);
            SetupDoor(game, door, doorBounds);

            CreatePlayer(game);
            CreateCollector(game);

            game.Ambience = gameObject.AddComponent<AmbienceController>();
            game.Ambience.Initialize();
            game.Ambience.Apply(game.Items.Count, game.Items.Count, true);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.30f, 0.285f, 0.26f);
            RenderSettings.ambientIntensity = 1f;
        }

        private void BuildRoom(Bounds doorBounds)
        {
            GameObject walls = new GameObject("Apartment_Walls");
            walls.transform.SetParent(transform, false);

            // Левая и правая стены.
            RuntimeAssets.Primitive(PrimitiveType.Cube, "Wall_Left", walls.transform,
                new Vector3(RoomLeft, RoomHeight * 0.5f, (RoomFront + RoomBack) * 0.5f),
                new Vector3(0.18f, RoomHeight, RoomFront - RoomBack), RuntimeAssets.Wall, true);
            RuntimeAssets.Primitive(PrimitiveType.Cube, "Wall_Right", walls.transform,
                new Vector3(RoomRight, RoomHeight * 0.5f, (RoomFront + RoomBack) * 0.5f),
                new Vector3(0.18f, RoomHeight, RoomFront - RoomBack), RuntimeAssets.Wall, true);

            // Передняя стена.
            RuntimeAssets.Primitive(PrimitiveType.Cube, "Wall_Front", walls.transform,
                new Vector3(0f, RoomHeight * 0.5f, RoomFront),
                new Vector3(RoomRight - RoomLeft, RoomHeight, 0.18f), RuntimeAssets.Wall, true);

            float gapMinX = doorBounds.min.x - 0.10f;
            float gapMaxX = doorBounds.max.x + 0.10f;
            float gapTop = Mathf.Clamp(doorBounds.max.y + 0.12f, 2.2f, RoomHeight - 0.1f);

            // Задняя стена с проёмом под картину-дверь.
            float leftWidth = gapMinX - RoomLeft;
            float rightWidth = RoomRight - gapMaxX;
            RuntimeAssets.Primitive(PrimitiveType.Cube, "Wall_Back_Left", walls.transform,
                new Vector3(RoomLeft + leftWidth * 0.5f, RoomHeight * 0.5f, RoomBack),
                new Vector3(leftWidth, RoomHeight, 0.18f), RuntimeAssets.Wall, true);
            RuntimeAssets.Primitive(PrimitiveType.Cube, "Wall_Back_Right", walls.transform,
                new Vector3(gapMaxX + rightWidth * 0.5f, RoomHeight * 0.5f, RoomBack),
                new Vector3(rightWidth, RoomHeight, 0.18f), RuntimeAssets.Wall, true);
            RuntimeAssets.Primitive(PrimitiveType.Cube, "Wall_Back_Top", walls.transform,
                new Vector3((gapMinX + gapMaxX) * 0.5f, (gapTop + RoomHeight) * 0.5f, RoomBack),
                new Vector3(gapMaxX - gapMinX, RoomHeight - gapTop, 0.18f), RuntimeAssets.Wall, true);
            if (doorBounds.min.y > 0.05f)
            {
                RuntimeAssets.Primitive(PrimitiveType.Cube, "Wall_Back_Bottom", walls.transform,
                    new Vector3((gapMinX + gapMaxX) * 0.5f, doorBounds.min.y * 0.5f, RoomBack),
                    new Vector3(gapMaxX - gapMinX, doorBounds.min.y, 0.18f), RuntimeAssets.Wall, true);
            }

            // Потолок.
            RuntimeAssets.Primitive(PrimitiveType.Cube, "Ceiling", walls.transform,
                new Vector3(0f, RoomHeight, (RoomFront + RoomBack) * 0.5f),
                new Vector3(RoomRight - RoomLeft, 0.18f, RoomFront - RoomBack), RuntimeAssets.Ceiling, true);

            // Тёмный тамбур за картиной: оттуда выходит коллектор.
            RuntimeAssets.Primitive(PrimitiveType.Cube, "Alcove_Back", walls.transform,
                new Vector3((gapMinX + gapMaxX) * 0.5f, 1.5f, RoomBack - 1.9f),
                new Vector3(gapMaxX - gapMinX + 0.4f, 3.0f, 0.18f), RuntimeAssets.Trim, true);
            RuntimeAssets.Primitive(PrimitiveType.Cube, "Alcove_Left", walls.transform,
                new Vector3(gapMinX - 0.18f, 1.5f, RoomBack - 0.95f),
                new Vector3(0.18f, 3.0f, 1.9f), RuntimeAssets.Trim, true);
            RuntimeAssets.Primitive(PrimitiveType.Cube, "Alcove_Right", walls.transform,
                new Vector3(gapMaxX + 0.18f, 1.5f, RoomBack - 0.95f),
                new Vector3(0.18f, 3.0f, 1.9f), RuntimeAssets.Trim, true);

            // Наличник вокруг проёма.
            float frameZ = RoomBack + 0.11f;
            RuntimeAssets.Primitive(PrimitiveType.Cube, "DoorFrame_Left", walls.transform,
                new Vector3(gapMinX, gapTop * 0.5f, frameZ), new Vector3(0.09f, gapTop, 0.06f), _frameMaterial, false);
            RuntimeAssets.Primitive(PrimitiveType.Cube, "DoorFrame_Right", walls.transform,
                new Vector3(gapMaxX, gapTop * 0.5f, frameZ), new Vector3(0.09f, gapTop, 0.06f), _frameMaterial, false);
            RuntimeAssets.Primitive(PrimitiveType.Cube, "DoorFrame_Top", walls.transform,
                new Vector3((gapMinX + gapMaxX) * 0.5f, gapTop, frameZ),
                new Vector3(gapMaxX - gapMinX + 0.09f, 0.09f, 0.06f), _frameMaterial, false);

            // Плинтусы.
            RuntimeAssets.Primitive(PrimitiveType.Cube, "Baseboard_Left", walls.transform,
                new Vector3(RoomLeft + 0.12f, 0.07f, (RoomFront + RoomBack) * 0.5f),
                new Vector3(0.06f, 0.14f, RoomFront - RoomBack), RuntimeAssets.Trim, false);
            RuntimeAssets.Primitive(PrimitiveType.Cube, "Baseboard_Right", walls.transform,
                new Vector3(RoomRight - 0.12f, 0.07f, (RoomFront + RoomBack) * 0.5f),
                new Vector3(0.06f, 0.14f, RoomFront - RoomBack), RuntimeAssets.Trim, false);
        }

        private void BuildLighting()
        {
            var sun = RuntimeAssets.Find("Directional Light");
            if (sun != null)
            {
                var light = sun.GetComponent<Light>();
                if (light != null)
                {
                    light.intensity = 0.55f;
                    light.color = new Color(1f, 0.94f, 0.84f);
                }
            }

            CreateCeilingLamp("Lamp_Warm_1", new Vector3(-4.2f, RoomHeight - 0.22f, -5.7f), 4.8f, 10f);
            CreateCeilingLamp("Lamp_Warm_2", new Vector3(2.8f, RoomHeight - 0.22f, -2.2f), 4.2f, 9f);
            CreateCeilingLamp("Lamp_Warm_3", new Vector3(-0.4f, RoomHeight - 0.22f, 1.2f), 3.8f, 9f);

            var deskLightGo = new GameObject("Lamp_Desk_Green");
            deskLightGo.transform.SetParent(transform, false);
            deskLightGo.transform.position = new Vector3(4.35f, 2.15f, -8.35f);
            var deskLight = deskLightGo.AddComponent<Light>();
            deskLight.type = LightType.Point;
            deskLight.color = new Color(0.55f, 1f, 0.68f);
            deskLight.intensity = 2.2f;
            deskLight.range = 4.4f;

            var alcoveLightGo = new GameObject("Lamp_Alcove");
            alcoveLightGo.transform.SetParent(transform, false);
            alcoveLightGo.transform.position = new Vector3(-3.32f, 2.2f, -11.3f);
            var alcoveLight = alcoveLightGo.AddComponent<Light>();
            alcoveLight.type = LightType.Point;
            alcoveLight.color = new Color(1f, 0.28f, 0.18f);
            alcoveLight.intensity = 1.2f;
            alcoveLight.range = 3.2f;
        }

        private void CreateCeilingLamp(string name, Vector3 position, float intensity, float range)
        {
            var root = new GameObject(name);
            root.transform.SetParent(transform, false);
            root.transform.position = position;

            RuntimeAssets.Primitive(PrimitiveType.Cylinder, name + "_Shade", root.transform,
                Vector3.zero, new Vector3(0.34f, 0.06f, 0.34f), RuntimeAssets.Trim, false);

            var bulb = RuntimeAssets.Primitive(PrimitiveType.Sphere, name + "_Bulb", root.transform,
                new Vector3(0f, -0.08f, 0f), Vector3.one * 0.16f, _lampMaterial, false);

            var light = root.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.86f, 0.66f);
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.Soft;
        }

        private void CreateFloorCollider()
        {
            // Коллайдер пола тянется и в тамбур, чтобы игрок не проваливался,
            // если выйдет за картину-дверь вслед за коллектором.
            var floor = new GameObject("Floor_Collider");
            floor.transform.SetParent(transform, false);
            floor.transform.position = new Vector3(0f, -0.08f, (RoomFront + RoomBack - 2.2f) * 0.5f);
            var box = floor.AddComponent<BoxCollider>();
            box.size = new Vector3(RoomRight - RoomLeft, 0.16f, RoomFront - (RoomBack - 2.2f));
        }

        private void AddComputer(GameObject cabinet, Bounds cabinetBounds, MonitorTerminal monitor)
        {
            if (cabinet == null) return;
            RuntimeAssets.AddBoxCollider(cabinet, cabinetBounds);
            var computer = cabinet.GetComponent<ComputerInteractable>();
            if (computer == null) computer = cabinet.AddComponent<ComputerInteractable>();
            computer.Monitor = monitor;
        }

        private void BuildSellables(GameManager game)
        {
            var entries = new (string name, string display)[]
            {
                ("armchair", "Кресло"),
                ("bed_single_A", "Кровать"),
                ("cabinet_small_decorated", "Шкафчик с декором"),
                ("cabinet_small", "Тумба"),
                ("book_set", "Стопка книг"),
                ("couch", "Диван"),
                ("table_medium", "Стол"),
                ("chair_B", "Стул"),
                ("chair_C", "Стул"),
                ("cactus_medium_A", "Кактус"),
                ("shelf_B_small_decorated", "Полка"),
                ("lamp_standing", "Торшер"),
            };

            foreach (var entry in entries)
            {
                var go = RuntimeAssets.Find(entry.name);
                if (go == null) continue;
                if (go.GetComponent<SellableItem>() != null) continue;

                Bounds bounds = RuntimeAssets.GetBounds(go);
                float volume = Mathf.Max(0.01f, bounds.size.x * bounds.size.y * bounds.size.z);
                const float pricePerCubicMeter = 60f;
                int price = Mathf.Max(15, Mathf.RoundToInt(volume * pricePerCubicMeter / 5f) * 5);

                RuntimeAssets.AddBoxCollider(go, bounds);
                var item = go.AddComponent<SellableItem>();
                item.Initialize(entry.display, price, volume);
                game.Items.Add(item);
            }
        }

        private void SetupDoor(GameManager game, GameObject door, Bounds doorBounds)
        {
            if (door == null) return;

            RuntimeAssets.AddBoxCollider(door, doorBounds);

            var pivot = new GameObject("Door_Pivot");
            pivot.transform.SetParent(transform, false);
            pivot.transform.position = new Vector3(doorBounds.min.x + 0.025f, doorBounds.center.y, doorBounds.center.z);
            pivot.transform.rotation = Quaternion.identity;

            door.transform.SetParent(pivot.transform, true);

            var doorInteractable = door.GetComponent<DoorInteractable>();
            if (doorInteractable == null) doorInteractable = door.AddComponent<DoorInteractable>();

            game.DoorPivot = pivot.transform;
            game.DoorClosedRotation = pivot.transform.rotation;
            game.DoorOpenRotation = Quaternion.Euler(0f, -104f, 0f) * pivot.transform.rotation;
            game.DoorBounds = doorBounds;
        }

        private void CreatePlayer(GameManager game)
        {
            var playerGo = new GameObject("Player");
            playerGo.transform.SetParent(transform, false);
            playerGo.transform.position = new Vector3(0f, 0.02f, -1.35f);
            playerGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            var controller = playerGo.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.3f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.skinWidth = 0.02f;
            controller.stepOffset = 0.25f;

            var player = playerGo.AddComponent<FirstPersonPlayer>();
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }

            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 100f;
            player.Initialize(cam);

            var interactor = playerGo.AddComponent<PlayerInteractor>();
            interactor.Player = player;
            interactor.Cam = cam;

            game.Player = player;
            game.Interactor = interactor;
            game.CameraDirector.Initialize(cam, player);
        }

        private void CreateCollector(GameManager game)
        {
            game.Collector = CollectorController.Create(game, game.DoorBounds);
        }
    }
}
