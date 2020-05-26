using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameStart : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        NetManager.Instance.Connect("106.12.218.138", 8011);
        StartCoroutine(NetManager.Instance.CheckNetType());
    }


    // Update is called once per frame
    void Update()
    {
        NetManager.Instance.Update();

        if (Input.GetKeyDown(KeyCode.A)) 
        {
            ProtocolMgr.SocketTest();
            ProtocolMgr.SocketTest();
            ProtocolMgr.SocketTest();
            ProtocolMgr.SocketTest();
        }

        if (Input.GetKeyDown(KeyCode.S)) 
        {
            ProtocolMgr.Register(RegisterType.Phone, "18569944325", "幻世界", "123456",(res) =>
            {
                if (res == RegisterResult.AlreadyExist)
                {
                    Debug.LogError("该手机号已经注册过了");
                }
                else if (res == RegisterResult.WrongCode) 
                {
                    Debug.LogError("验证码错误");
                }
                else if (res == RegisterResult.Forbidden)
                {
                    Debug.LogError("改账户禁止注册，联系客服！");
                }
                else if (res == RegisterResult.Success)
                {
                    Debug.Log("注册成功");
                }
            });
        }

        if (Input.GetKeyDown(KeyCode.D)) 
        {
            ProtocolMgr.Login( LoginType.Phone, "18569944325", "幻世",(res,restoken)=> 
            {
                if (res == LoginResult.Success)
                {
                    Debug.Log("登录成功");
                }
                else if (res == LoginResult.Failed) 
                {
                    Debug.LogError("登录失败");
                }
                else if (res == LoginResult.WrongPwd)
                {
                    Debug.LogError("密码错误");
                }
                else if (res == LoginResult.UserNotExist)
                {
                    Debug.LogError("用户不存在");
                }
                else if (res == LoginResult.TimeoutToken)
                {
                    Debug.LogError("Token已过期");
                }
            });
        }
    }

    private void OnApplicationQuit()
    {
        NetManager.Instance.ClientClose();
    }

}
