using UnityEngine;
 //随便写的凑合用
public class PlayerMoveLogic : MonoBehaviour
{
    public GameObject Player;
    public float m_speed = 5f;
 
    void Start()
    {
        if (Player == null)
        {
            Player = this.gameObject;
        }
    }
    void Update()
    {
        //键盘控制移动
        PlayerMove_KeyTransform();
    }
 
    //通过Transform组件 键盘控制移动
    public void PlayerMove_KeyTransform()
    {
        if (Input.GetKey(KeyCode.W) | Input.GetKey(KeyCode.UpArrow)) //前
        {
            Player.transform.Translate(Vector3.forward * m_speed * Time.deltaTime);
        }
        if (Input.GetKey(KeyCode.S) | Input.GetKey(KeyCode.DownArrow)) //后
        {
            Player.transform.Translate(Vector3.forward * -m_speed * Time.deltaTime);
        }
        if (Input.GetKey(KeyCode.A) | Input.GetKey(KeyCode.LeftArrow)) //左
        {
            Player.transform.Translate(Vector3.right * -m_speed * Time.deltaTime);
        }
        if (Input.GetKey(KeyCode.D) | Input.GetKey(KeyCode.RightArrow)) //右
        {
            Player.transform.Translate(Vector3.right * m_speed * Time.deltaTime);
        }
    }
}
