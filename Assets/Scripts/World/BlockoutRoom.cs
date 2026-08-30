using UnityEngine;

namespace DungeonCrawler.World
{
    // Crude blockout for the abyss test room -- a flat floor with 4 walls, sized to fit
    // GameBootstrap's spawn positions. Not a dungeon generator, just enough geometry
    // that movement/aggro/AoE have a bounded space to be tested in.
    public class BlockoutRoom : MonoBehaviour
    {
        public float width = 20f;
        public float depth = 20f;
        public float wallHeight = 3f;
        public Color floorColor = new Color(0.15f, 0.05f, 0.08f); // dark abyss red-black
        public Color wallColor = new Color(0.08f, 0.02f, 0.04f);

        private void Start()
        {
            BuildFloor();
            BuildWall(new Vector3(0, wallHeight / 2f, depth / 2f), new Vector3(width, wallHeight, 0.5f));
            BuildWall(new Vector3(0, wallHeight / 2f, -depth / 2f), new Vector3(width, wallHeight, 0.5f));
            BuildWall(new Vector3(width / 2f, wallHeight / 2f, 0), new Vector3(0.5f, wallHeight, depth));
            BuildWall(new Vector3(-width / 2f, wallHeight / 2f, 0), new Vector3(0.5f, wallHeight, depth));
        }

        private void BuildFloor()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "AbyssFloor";
            floor.transform.SetParent(transform);
            floor.transform.localScale = new Vector3(width / 10f, 1f, depth / 10f); // default Plane is 10x10
            SetColor(floor, floorColor);
        }

        private void BuildWall(Vector3 localPos, Vector3 scale)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "AbyssWall";
            wall.transform.SetParent(transform);
            wall.transform.localPosition = localPos;
            wall.transform.localScale = scale;
            SetColor(wall, wallColor);
        }

        private void SetColor(GameObject go, Color c)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                // Uses a fresh material instance so rooms don't share/overwrite each other's color.
                renderer.material = new Material(Shader.Find("Standard")) { color = c };
            }
        }
    }
}
