using Unity.Entities;
using UnityEngine;

public class ScreenToWorldCursorBridge : MonoBehaviour
{
    private EntityManager em;
    private Entity cursorEntity;
    private Camera cam;

    private void Awake()
    {
        em = World.DefaultGameObjectInjectionWorld.EntityManager;
        cam = GetComponent<Camera>();

        var query = em.CreateEntityQuery(typeof(CursorWorldPosition));
        cursorEntity = query.GetSingletonEntity();
    }

    private void Update()
    {
        Vector3 cursorOnScreen = Input.mousePosition;
        cursorOnScreen.z = -cam.transform.position.z;

        Vector3 cursorInWorld = cam.ScreenToWorldPoint(cursorOnScreen);

        em.SetComponentData(cursorEntity, new CursorWorldPosition
        {
            Value = new (cursorInWorld.x, cursorOnScreen.y)
        });
    }
}
