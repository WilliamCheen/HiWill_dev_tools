using Main.Network;

public enum AppEnv
{
    Dev,
    Test,
    Release
}


public class AppStartConfig
{
    public readonly string BaseUrl;
    public readonly string LoginKey;
    public readonly string Platform;
    public readonly string SignKey;
    public readonly string Scheme;
    public readonly AppEnv Env;
    public readonly URLSchemeParameterEntity urlSchemeParams;

    public static AppStartConfig Current { get; private set; }

    //public const string MinIOPoint = "http://192.168.1.190:9000/release-unity/hot-update/hiwill-mac-address-tool";
    public const string MinIOPoint = "https://test-minio.labsonline.cn/release-unity/hot-update/hiwill-mac-address-tool";

    /// <summary>
    /// 从 URLScheme 参数里获取实例
    /// </summary>
    /// <param name="launchParams"></param>
    public AppStartConfig(URLSchemeParameterEntity launchParams)
    {
        urlSchemeParams = launchParams;
        Platform = launchParams.platform;
        SignKey = launchParams.signKey;
        switch (launchParams.env)
        {
            case "dev":
                Env = AppEnv.Dev;
                BaseUrl = MinIOPoint;
                break;
            case "test":
                Env = AppEnv.Test;
                BaseUrl = MinIOPoint;
                break;
            default:
                Env = AppEnv.Release;
                BaseUrl = MinIOPoint;
                break;
        }

        Current = this;
    }


    /// <summary>
    /// 从环境中获取 Env
    /// </summary>
    /// <param name="fromEnv"></param>
    public AppStartConfig(AppEnv fromEnv)
    {
        Platform = "TCM";
        switch (fromEnv)
        {
            case AppEnv.Dev:
                BaseUrl = MinIOPoint;
                LoginKey =
                    "MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQCHiGusGBZybYwoPfES6GFJWm/L6kM/bp0MZJhRJ18yi7pnXyFLuhgz0yHRCej0mZRHgeHM+tJkGF4FErofGqtGjdU5vCgCb56OzaF0Fyq4FnDZF9T/XxWBkDqSGjhBDvvshdM5bQ9knZbMDbcOFzJA/rP7H9oA87LpTj0egh8rNwIDAQAB";
                SignKey = "pp5ymssp6kdlw8dat0sfpcysft5l7tai";
                break;
            case AppEnv.Test:
                BaseUrl = MinIOPoint;
                LoginKey =
                    "MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQCSL15PGw9ulcu8NV9sPtLjG6o6Tvj0jHxYB3j6hVx4boVKhtlCGFRY/6mEQZ6uFafZTpq8MGU+qpAoSSVR0EsDYBQ9duAMagEVqoftix2bApMt1X+VOnst6LfrC3KrBv8XZH9X6IP9fghYXhqfztXPNZvl2UcP2fb0yuVmpLjQMQIDAQAB";
                SignKey = "tvadan6jb5287uzmh491d6pehem3f6ih";
                break;
            default:
                BaseUrl = MinIOPoint;
                LoginKey =
                    "MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQCYyRZuwPsmeeRZbaY1WmsMcsjXHzwN6pZFkFG8fjhUyg8QwMHaCSPjTaq4EMkyEYntiHXzCGQZijvBSmK1bnOO1sY/2K94rvmR6vZImI6uRyfLH8u6LhfKgfUAr4ubPzhdZDtHMA0ft5jkELm8D/UvRewgephdzBW+4BMBOYNRiwIDAQAB";
                SignKey = "9ob7lmyrdk8y9yrl62lj21y4sje6xa22";
                break;
        }

        Current = this;
    }
}


//
//mtss://?
//platform=WEB&
//token=eyJhbGciOiJIUzUxMiJ9.eyJyYW5kb21LZXkiOiJpdmlxa3YiLCJzdWIiOiJ7XCJpZFwiOlwiODcxZjVhNTc2NjcxNGJiMWJiODk3ZGMyM2VjZjc1MDlcIixcImluc0lkXCI6XCIxMFwiLFwiaW5zVHlwZVwiOjIsXCJ1c2VySWRcIjpcIjE5ODdmNjJkMjgyNTRiMWE5MGM5YWFjODI3MGU2ZDg0XCJ9IiwiZXhwIjoxNzY0OTIyNDY2LCJpYXQiOjE3NjQwNTg0NjZ9.jSNvNObo9Ym4rhxV8XaM0CrWPE_XFXWP2Y7uYb66_ShizN-jgcCecHQhFfptNGwsG539v2Ou0xLNQ4cifsdhTA&
//signKey=8ioxiyztczjdt18k9c8vd6f2w9jlm6cb&type=1&
//env=test&
//date=1764058484159&
//scheme=mtsctt