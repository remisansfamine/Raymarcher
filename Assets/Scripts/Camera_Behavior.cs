using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Camera_Behavior : MonoBehaviour
{
    [Min(0)]
    public float hspeed, vspeed, zspeed, moveSpeed, scrollWheelSpeed;
    float vmove = 0, hmove = 0, zmove = 0;

    Vector3 directionToMove;

    void Update()
    {
        hmove += hspeed * Input.GetAxis("Mouse X");

        vmove -= vspeed * Input.GetAxis("Mouse Y");

        zmove -= zspeed * Input.GetAxis("Lean");

        moveSpeed += scrollWheelSpeed * Input.GetAxis("Mouse ScrollWheel");

        transform.rotation = Quaternion.Euler(vmove, hmove, zmove);

        directionToMove = transform.rotation * new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
    }

    public void FixedUpdate()
    {
        GetComponent<Rigidbody>().MovePosition(transform.position + directionToMove * moveSpeed * Time.fixedDeltaTime);
    }
}
