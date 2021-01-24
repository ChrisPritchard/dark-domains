
namespace DarkDomains
{
    using UnityEngine;
    
    public class HexGridChunk : MonoBehaviour 
    {
        HexCell[] cells;
        Canvas canvas;
        
        public HexMesh Terrain;

        private void Awake() 
        {
            canvas = GetComponentInChildren<Canvas>();

            cells = new HexCell[HexMetrics.ChunkSizeX * HexMetrics.ChunkSizeZ];
        }

        public void AddCell(int index, HexCell cell)
        {
            cells[index] = cell;
            cell.transform.SetParent(this.transform, false);
            cell.UIRect.SetParent(canvas.transform, false);
            cell.Chunk = this;
        }

        // chunks are only enabled when they need to triangulate
        public void Refresh()
        {
            enabled = true;
        }

        // this method will only be invoked if the chunk is enabled, and will then disable itself
        private void LateUpdate() 
        {
            Triangulate(cells); 
            enabled = false;
        }

        public void ShowUI(bool visible) => canvas.gameObject.SetActive(visible);

        public void Triangulate(HexCell[] cells)
        {
            Terrain.Clear();

            foreach(var cell in cells)
                Triangulate(cell);

            Terrain.Apply();
        }

        private void Triangulate(HexCell cell)
        {
            for (var d = HexDirection.NE; d <= HexDirection.NW; d++)
                Triangulate(d, cell);
        }

        // triangulates one of the six cores of a hex cell
        // and, if the conditions are met, the bridge and corner on that side
        private void Triangulate(HexDirection direction, HexCell cell)
        {
            var e = new EdgeVertices(
                cell.Position + HexMetrics.GetFirstSolidCorner(direction),
                cell.Position + HexMetrics.GetSecondSolidCorner(direction)
            );
                
            if (cell.HasRiver)
            {
                if (cell.HasRiverThroughEdge(direction))
                {
                    e.v3.y = cell.StreamBedY;
                    if(cell.HasRiverBeginOrEnd)
                        TriangulateWithRiverBeginOrEnd(direction, cell, cell.Position, e);
                    else
                        TriangulateWithRiver(direction, cell, cell.Position, e);
                }
                else
                    TriangulateAdjacentToRiver(direction, cell, cell.Position, e);
            }
            else
                TriangulateEdgeFan(cell.Position, e, cell.Colour);

            if(direction <= HexDirection.SE)
                TriangulateConnection(direction, cell, e);
        }

        private void TriangulateWithRiverBeginOrEnd(HexDirection direction, HexCell cell, Vector3 centre, EdgeVertices e)
        {
            var m = new EdgeVertices(
                Vector3.Lerp(centre, e.v1, 0.5f),
                Vector3.Lerp(centre, e.v5, 0.5f));
            m.v3.y = e.v3.y;

            TriangulateEdgeStrip(m, cell.Colour, e, cell.Colour);
            TriangulateEdgeFan(centre, m, cell.Colour);
        }

        private void TriangulateWithRiver(HexDirection direction, HexCell cell, Vector3 centre, EdgeVertices e)
        {
            Vector3 centreL, centreR;
            if (cell.HasRiverThroughEdge(direction.Opposite()))
            {
                centreL = centre + HexMetrics.GetFirstSolidCorner(direction.Previous()) * 0.25f;
                centreR = centre + HexMetrics.GetSecondSolidCorner(direction.Next()) * 0.25f;
            }
            else if (cell.HasRiverThroughEdge(direction.Next()))
            {
                centreL = centre;
                centreR = Vector3.Lerp(centre, e.v5, 2f/3);
            }
            else if (cell.HasRiverThroughEdge(direction.Previous()))
            {
                centreL = Vector3.Lerp(centre, e.v1, 2f/3);
                centreR = centre;
            }
            else if (cell.HasRiverThroughEdge(direction.Next2()))
            {
                centreL = centre;
                centreR = centre + HexMetrics.GetSolidEdgeMiddle(direction.Next()) * 0.5f * HexMetrics.InnerToOuter;
            }
            else
            {
                centreL = centre + HexMetrics.GetSolidEdgeMiddle(direction.Previous()) * 0.5f * HexMetrics.InnerToOuter;
                centreR = centre;
            }

            centre = Vector3.Lerp(centreL, centreR, 0.5f); // aligns edges

            var m = new EdgeVertices(
                Vector3.Lerp(centreL, e.v1, 0.5f),
                Vector3.Lerp(centreR, e.v5, 0.5f),
                1f/6);
            m.v3.y = centre.y = e.v3.y;

            TriangulateEdgeStrip(m, cell.Colour, e, cell.Colour);

            Terrain.AddTriangle(centreL, m.v1, m.v2);
            Terrain.AddTriangleColour(cell.Colour);

            Terrain.AddQuad(centreL, centre, m.v2, m.v3);
            Terrain.AddQuadColour(cell.Colour);

            Terrain.AddQuad(centre, centreR, m.v3, m.v4);
            Terrain.AddQuadColour(cell.Colour);

            Terrain.AddTriangle(centreR, m.v4, m.v5);
            Terrain.AddTriangleColour(cell.Colour);
        }

        private void TriangulateAdjacentToRiver(HexDirection direction, HexCell cell, Vector3 centre, EdgeVertices e)
        {
            if (cell.HasRiverThroughEdge(direction.Next()))
            {
                if(cell.HasRiverThroughEdge(direction.Previous())) // on a curve, so pull back the centre point
                    centre += HexMetrics.GetSolidEdgeMiddle(direction) * HexMetrics.InnerToOuter * 0.5f;
                else if (cell.HasRiverThroughEdge(direction.Previous2())) // straight connection - pull to one side
                    centre += HexMetrics.GetFirstSolidCorner(direction) * 0.25f;
            }
            else if (cell.HasRiverThroughEdge(direction.Previous()) && cell.HasRiverThroughEdge(direction.Next2()))
                centre += HexMetrics.GetSecondSolidCorner(direction) * 0.25f; // other type of straight connection

            var m = new EdgeVertices(
                Vector3.Lerp(centre, e.v1, 0.5f),
                Vector3.Lerp(centre, e.v5, 0.5f));

            TriangulateEdgeStrip(m, cell.Colour, e, cell.Colour);
            TriangulateEdgeFan(centre, m, cell.Colour);
        }

        // adds bridges and corner triangles
        private void TriangulateConnection(HexDirection direction, HexCell cell, EdgeVertices e)
        {
            var neighbour = cell.GetNeighbour(direction);
            if (neighbour == null)
                return; // dont add for edge hexes

            var bridge = HexMetrics.GetBridge(direction);
            bridge.y = neighbour.Position.y - cell.Position.y;
            var e2 = new EdgeVertices(
                e.v1 + bridge,
                e.v5 + bridge
            );

            if (cell.HasRiverThroughEdge(direction))
                e2.v3.y = neighbour.StreamBedY;

            if (HexMetrics.GetEdgeType(cell.Elevation, neighbour.Elevation) == HexEdgeType.Slope)
                TriangulateEdgeTerrace(e, cell, e2, neighbour);
            else
                TriangulateEdgeStrip(e, cell.Colour, e2, neighbour.Colour);

            if(direction > HexDirection.E)
                return;
            var nextDirection = direction.Next();
            var nextNeighbour = cell.GetNeighbour(nextDirection);
            if (nextNeighbour == null)
                return;

            var v5 = e.v5 + HexMetrics.GetBridge(nextDirection);
            v5.y = nextNeighbour.Position.y;
            
            var minElevation = Mathf.Min(cell.Elevation, neighbour.Elevation, nextNeighbour.Elevation);
            if (minElevation == cell.Elevation)
                TriangulateCorner(e.v5, cell, e2.v5, neighbour, v5, nextNeighbour);
            else if (minElevation == neighbour.Elevation)
                TriangulateCorner(e2.v5, neighbour, v5, nextNeighbour, e.v5, cell);
            else
                TriangulateCorner(v5, nextNeighbour, e.v5, cell, e2.v5, neighbour);
        }

        private void TriangulateEdgeTerrace(EdgeVertices begin, HexCell beginCell, EdgeVertices end, HexCell endCell)
        {
            var es = begin;
            var c1 = beginCell.Colour;
            
            for(var step = 0; step <= HexMetrics.TerraceSteps; step++)
            {
                var ed = EdgeVertices.TerraceLerp(begin, end, step);
                var c2 = HexMetrics.TerraceLerp(beginCell.Colour, endCell.Colour, step);
                TriangulateEdgeStrip(es, c1, ed, c2);
                es = ed; c1 = c2;
            }
        }

        private void TriangulateCorner(
            Vector3 bottom, HexCell bottomCell,
            Vector3 left, HexCell leftCell,
            Vector3 right, HexCell rightCell)
        {
            var leftEdge = bottomCell.GetEdgeType(leftCell);
            var rightEdge = bottomCell.GetEdgeType(rightCell);

            if (leftEdge == HexEdgeType.Slope)
            {
                if (rightEdge == HexEdgeType.Slope) // SSF: slope-slope-flat
                    TriangulateCornerTerraces(bottom, bottomCell, left, leftCell, right, rightCell);
                else if (rightEdge == HexEdgeType.Flat) // SFS: slope-flat-slope
                    TriangulateCornerTerraces(left, leftCell, right, rightCell, bottom, bottomCell);
                else
                    TriangulateCornerTerracesCliff(bottom, bottomCell, left, leftCell, right, rightCell);
            } 
            else if (rightEdge == HexEdgeType.Slope)
            {
                if (leftEdge == HexEdgeType.Flat) // FSS: flat-slope-slope
                    TriangulateCornerTerraces(right, rightCell, bottom, bottomCell, left, leftCell);
                else // must be a cliff, as left as slope has already been covered
                    TriangulateCornerCliffTerraces(bottom, bottomCell, left, leftCell, right, rightCell);
            }
            else if (leftCell.GetEdgeType(rightCell) == HexEdgeType.Slope) // both sides are cliffs, top is slope
            {
                if (leftCell.Elevation < rightCell.Elevation)
                    TriangulateCornerCliffTerraces(right, rightCell, bottom, bottomCell, left, leftCell); // CCSR: cliff, cliff, slope to right
                else
                    TriangulateCornerTerracesCliff(left, leftCell, right, rightCell, bottom, bottomCell); // CCSL: cliff, cliff, slope to left
            }
            else // no terraces anywhere, so a simple triangle will do
            {
                Terrain.AddTriangle(bottom, left, right);
                Terrain.AddTriangleColour(bottomCell.Colour, leftCell.Colour, rightCell.Colour);
            }
        }

        private void TriangulateCornerTerraces(
            Vector3 bottom, HexCell bottomCell,
            Vector3 left, HexCell leftCell,
            Vector3 right, HexCell rightCell)
        {
            var v1 = bottom;
            var v2 = v1;
            var c1 = bottomCell.Colour;
            var c2 = c1;

            for(var step = 0; step <= HexMetrics.TerraceSteps; step++)
            {
                var v3 = HexMetrics.TerraceLerp(bottom, left, step);
                var v4 = HexMetrics.TerraceLerp(bottom, right, step);
                var c3 = HexMetrics.TerraceLerp(bottomCell.Colour, leftCell.Colour, step);
                var c4 = HexMetrics.TerraceLerp(bottomCell.Colour, rightCell.Colour, step);

                if(step == 0)
                {
                    Terrain.AddTriangle(bottom, v3, v4);
                    Terrain.AddTriangleColour(bottomCell.Colour, c3, c4);
                    continue;
                }

                Terrain.AddQuad(v1, v2, v3, v4);
                Terrain.AddQuadColour(c1, c2, c3, c4);
                v1 = v3; v2 = v4; c1 = c3; c2 = c4;
            }
        }

        private void TriangulateCornerTerracesCliff(
            Vector3 bottom, HexCell bottomCell,
            Vector3 left, HexCell leftCell,
            Vector3 right, HexCell rightCell)
        {
            var b = Mathf.Abs(1f / (rightCell.Elevation - bottomCell.Elevation));
            var boundary = Vector3.Lerp(HexMetrics.Perturb(bottom), HexMetrics.Perturb(right), b);
            var boundaryColour = Color.Lerp(bottomCell.Colour, rightCell.Colour, b);

            TriangulteBoundaryTriangle(bottom, bottomCell, left, leftCell, boundary, boundaryColour);
            TriangulateTop(left, leftCell, right, rightCell, boundary, boundaryColour);
        }

        private void TriangulateCornerCliffTerraces(
            Vector3 bottom, HexCell bottomCell,
            Vector3 left, HexCell leftCell,
            Vector3 right, HexCell rightCell)
        {
            var b = Mathf.Abs(1f / (leftCell.Elevation - bottomCell.Elevation));
            var boundary = Vector3.Lerp(HexMetrics.Perturb(bottom), HexMetrics.Perturb(left), b);
            var boundaryColour = Color.Lerp(bottomCell.Colour, leftCell.Colour, b);

            TriangulteBoundaryTriangle(right, rightCell, bottom, bottomCell, boundary, boundaryColour);
            TriangulateTop(left, leftCell, right, rightCell, boundary, boundaryColour);
        }

        private void TriangulateTop(
            Vector3 left, HexCell leftCell,
            Vector3 right, HexCell rightCell,
            Vector3 boundary, Color boundaryColour)
        {
            if(rightCell.GetEdgeType(leftCell) == HexEdgeType.Slope)
            {
                TriangulteBoundaryTriangle(left, leftCell, right, rightCell, boundary, boundaryColour);
                return;
            }
            
            Terrain.AddTriangleUnperturbed(HexMetrics.Perturb(left), HexMetrics.Perturb(right), boundary);
            Terrain.AddTriangleColour(leftCell.Colour, rightCell.Colour, boundaryColour);
        }

        private void TriangulteBoundaryTriangle(
            Vector3 bottom, HexCell bottomCell, 
            Vector3 terrace, HexCell terraceCell,
            Vector3 boundary, Color boundaryColour)
        {
            var v1 = HexMetrics.Perturb(bottom);
            var c1 = bottomCell.Colour;

            for(var step = 0; step <= HexMetrics.TerraceSteps; step++)
            {
                var v2 = HexMetrics.Perturb(HexMetrics.TerraceLerp(bottom, terrace, step));
                var c2 = HexMetrics.TerraceLerp(bottomCell.Colour, terraceCell.Colour, step);

                Terrain.AddTriangleUnperturbed(v1, v2, boundary);
                Terrain.AddTriangleColour(c1, c2, boundaryColour);

                v1 = v2; c1 = c2;
            }
        }

        private void TriangulateEdgeFan(Vector3 center, EdgeVertices edge, Color color)
        {
            Terrain.AddTriangle(center, edge.v1, edge.v2);
            Terrain.AddTriangleColour(color);
            Terrain.AddTriangle(center, edge.v2, edge.v3);
            Terrain.AddTriangleColour(color);
            Terrain.AddTriangle(center, edge.v3, edge.v4);
            Terrain.AddTriangleColour(color);
            Terrain.AddTriangle(center, edge.v4, edge.v5);
            Terrain.AddTriangleColour(color);
        }

        private void TriangulateEdgeStrip(EdgeVertices e1, Color c1, EdgeVertices e2, Color c2)
        {
            Terrain.AddQuad(e1.v1, e1.v2, e2.v1, e2.v2);
            Terrain.AddQuadColour(c1, c2);
            Terrain.AddQuad(e1.v2, e1.v3, e2.v2, e2.v3);
            Terrain.AddQuadColour(c1, c2);
            Terrain.AddQuad(e1.v3, e1.v4, e2.v3, e2.v4);
            Terrain.AddQuadColour(c1, c2);
            Terrain.AddQuad(e1.v4, e1.v5, e2.v4, e2.v5);
            Terrain.AddQuadColour(c1, c2);
        }
    }
}