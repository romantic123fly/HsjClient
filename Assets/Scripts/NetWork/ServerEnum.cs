﻿public enum RegisterType
{
    Phone,
    Mail,
}

public enum LoginType
{
    Phone,
    Mail,
    WX,
    QQ,
    Token,
}

public enum RegisterResult
{
    Success,
    Failed,
    AlreadyExist,
    WrongCode,
    Forbidden,
}

public enum LoginResult
{
    Success,
    Failed,
    AlreadyLogin,
    WrongPwd,
    UserNotExist,
    TimeoutToken
}

