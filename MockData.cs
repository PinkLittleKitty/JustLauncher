using System.Collections.Generic;

namespace JustLauncher;

public static class MockData
{
    public static List<Account> GetAccounts()
    {
        return new List<Account>
        {
            new Account { Username = "Player1", AccountType = "Offline", IsActive = true },
            new Account { Username = "Steve", AccountType = "Offline", IsActive = false }
        };
    }

    public static List<Installation> GetInstallations()
    {
        return new List<Installation>
        {
            new Installation { Name = "Latest Release", Version = "1.21.1" },
            new Installation { Name = "Modded 1.20.1", Version = "1.20.1", IsModded = true, ModLoader = "Fabric" }
        };
    }
}
