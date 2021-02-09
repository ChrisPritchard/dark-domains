
namespace DarkDomains
{
    using UnityEngine;
    using System.Collections.Generic;
    
    public class HexCellShaderData : MonoBehaviour 
    {
        Texture2D cellTexture;
        Color32[] cellTextureData;

        public bool ImmediateMode { get; set; } = true;

        List<HexCell> transitioningCells = new List<HexCell>();
        const float transitionSpeed = 255f;

        public void Initialise(int x, int z)
        {
            if(cellTexture)
                cellTexture.Resize(x, z);
            else
            {
                cellTexture = new Texture2D(x, z, TextureFormat.RGBA32, false, true);
                cellTexture.filterMode = FilterMode.Point;
                cellTexture.wrapMode = TextureWrapMode.Clamp;
                Shader.SetGlobalTexture("_HexCellData", cellTexture);
            }

            Shader.SetGlobalVector("_HexCellData_TexelSize", new Vector4(1f / x, 1f / z, x, z));

            if(cellTextureData == null || cellTextureData.Length != x * z)
                cellTextureData = new Color32[x * z];
            else
                for(var i = 0; i < cellTextureData.Length; i++)
                    cellTextureData[i] = new Color32(0, 0, 0, 0);

            transitioningCells.Clear();
            enabled = true;
        }

        public void RefreshTerrain(HexCell cell)
        {
            cellTextureData[cell.Index].a = cell.TerrainTypeIndex;
            enabled = true;
        }

        public void RefreshVisibility(HexCell cell)
        {
            if(ImmediateMode)
            {
                cellTextureData[cell.Index].r = (byte)(cell.IsVisible ? 255 : 0);
                cellTextureData[cell.Index].g = (byte)(cell.IsExplored ? 255 : 0);
            }
            else if (cellTextureData[cell.Index].b != 255)
            {   
                cellTextureData[cell.Index].b = 255; // just a flag to prevent re-adding
                transitioningCells.Add(cell);
            }
            enabled = true;
        }

        private void LateUpdate() 
        {
            var delta = (int)(Time.deltaTime * transitionSpeed);
            if(delta == 0)
                delta = 1;

            var next = new List<HexCell>(transitioningCells.Capacity);
            foreach(var cell in transitioningCells)
            {
                if(UpdateCellData(cell, delta))
                    next.Add(cell);
            }
            transitioningCells = next;

            cellTexture.SetPixels32(cellTextureData);
            cellTexture.Apply();
            enabled = transitioningCells.Count > 0;
        }

        private bool UpdateCellData(HexCell cell, int delta)
        {
            var data = cellTextureData[cell.Index];
            var stillUpdating = false;

            if(cell.IsExplored && data.g < 255)
            {
                stillUpdating = true;
                var t = data.g + delta;
                data.g = (byte)(t >= 255 ? 255 : t);
            }

            if(cell.IsVisible)
            {
                if(data.r < 255)
                {
                    stillUpdating = true;
                    var t = data.r + delta;
                    data.r = (byte)(t >= 255 ? 255 : t);
                }
            }
            else if (data.r > 0)
            {
                stillUpdating = true;
                var t = data.r - delta;
                data.r = (byte)(t < 0 ? 0 : t);
            }

            if(!stillUpdating)
                data.b = 0; // clear updating flag
            cellTextureData[cell.Index] = data;
            return stillUpdating;
        }
    }
}