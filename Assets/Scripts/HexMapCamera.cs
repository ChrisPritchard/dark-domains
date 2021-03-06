
namespace HexMapTutorials
{
    using UnityEngine;
    
    public class HexMapCamera : MonoBehaviour 
    {
        Transform swivel, stick;

        float zoom;
        float rotationAngle;

        public HexGrid Grid;

        public float StickMinZoom, StickMaxZoom;
        public float MoveSpeedMinZoom, MoveSpeedMaxZoom;
        public float RotationSpeed;

        public bool Locked = false;

        private void Awake() 
        {
            swivel = transform.GetChild(0);
            stick = swivel.transform.GetChild(0);

            AdjustZoom(0.8f);
        }

        private void OnEnable() 
        {
            ValidatePosition();
        }

        private void Update() 
        {
            if(Locked)
                return;

            var zoomDelta = Input.GetAxis("Mouse ScrollWheel");
            if (zoomDelta != 0f) 
                AdjustZoom(zoomDelta);

            var xDelta = Input.GetAxis("Horizontal");
            var zDelta = Input.GetAxis("Vertical");
            if (xDelta != 0f || zDelta != 0f)
                AdjustPosition(xDelta, zDelta);

            var rotationDelta = Input.GetAxis("Rotation");
            if (rotationDelta != 0f)
                AdjustRotation(rotationDelta);
        }

        private void AdjustZoom(float zoomDelta)
        {
            zoom = Mathf.Clamp01(zoom + zoomDelta);
            var distance = Mathf.Lerp(StickMinZoom, StickMaxZoom, zoom);
            stick.localPosition = new Vector3(0f, 0f, distance);
        }

        private void AdjustPosition(float xDelta, float zDelta)
        {
            var direction = transform.localRotation * new Vector3(xDelta, 0f, zDelta).normalized;
            var damping = Mathf.Max(Mathf.Abs(xDelta), Mathf.Abs(zDelta));
            var distance = Mathf.Lerp(MoveSpeedMinZoom, MoveSpeedMaxZoom, zoom) * Time.deltaTime;

            var position = transform.localPosition;
            position += direction * damping * distance;
            transform.localPosition = Grid.Wrapping ? WrapPosition(position) : ClampPosition(position);
        }

        private Vector3 WrapPosition(Vector3 position)
        {
            // this code will ensure the camera stays within the map width
            // without clamping - it just teleports the camera when it goes over a min/max
            var width = Grid.CellCountX * HexMetrics.InnerDiameter;
            while(position.x < 0f)
                position.x += width;
            while(position.x > width)
                position.x -= width;

            var zMax = (Grid.CellCountZ - 1f) * (1.5f * HexMetrics.OuterRadius);
            position.z = Mathf.Clamp(position.z, 0, zMax);

            Grid.CentreMap(position.x);
            return position;
        }

        private Vector3 ClampPosition(Vector3 position)
        {
            var XMax = (Grid.CellCountX - 0.5f) * HexMetrics.InnerDiameter;
            position.x = Mathf.Clamp(position.x, 0f, XMax);

            var ZMax = (Grid.CellCountZ - 1f) * 1.5f * HexMetrics.OuterRadius;
            position.z = Mathf.Clamp(position.z, 0f, ZMax);

            return position;
        }

        private void AdjustRotation(float delta)
        {
            rotationAngle += -delta * RotationSpeed * Time.deltaTime; // minus delta inverts movement - feels more natural to me
            if (rotationAngle < 0f)
                rotationAngle += 360f;
            else if (rotationAngle >= 360f)
                rotationAngle -= 360f;
            transform.localRotation = Quaternion.Euler(0f, rotationAngle, 0f);
        }

        public void ValidatePosition() => AdjustPosition(0, 0); // will trigger a clamp if outside of camera bounds
    }
}