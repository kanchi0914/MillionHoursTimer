using System;
using System.Runtime.InteropServices;

namespace CustomAction
{
    static class Remover
    {
        //ref:https://stackoverflow.com/questions/39261491/write-registry-to-hkey-current-user-instead-of-hkey-users
        public static void RemoveKey()
        {
            try
            {
                Microsoft.Win32.RegistryKey key;
                var SID = GetLoggedOnUserSID();
                var path = @"\Software\Microsoft\Windows\CurrentVersion\Run";
                key = Microsoft.Win32.Registry.Users.CreateSubKey(SID + path);

                key.DeleteValue(MHTimer.Settings.ProductName, false);
                key.Close();
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show("レジストリキーを削除時にエラーが発生しました:" + ex.ToString());
            }

        }

        enum TokenInformationClass
        {
            TokenOwner = 4,
        }

        struct TokenOwner
        {
            public IntPtr Owner;
        }

        [DllImport("advapi32.dll", EntryPoint = "GetTokenInformation", SetLastError = true)]
        static extern bool GetTokenInformation(
            IntPtr tokenHandle,
            TokenInformationClass tokenInformationClass,
            IntPtr tokenInformation,
            int tokenInformationLength,
            out int ReturnLength);

        [DllImport("kernel32.dll")]
        private static extern UInt32 WTSGetActiveConsoleSessionId();

        [DllImport("wtsapi32.dll", SetLastError = true)]
        static extern bool WTSQueryUserToken(UInt32 sessionId, out IntPtr Token);

        [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern bool ConvertSidToStringSid(IntPtr sid, [In, Out, MarshalAs(UnmanagedType.LPTStr)] ref string pStringSid);

        static string GetLoggedOnUserSID()
        {
            IntPtr tokenOwnerPtr;
            int tokenSize;
            IntPtr hToken;

            // Get a token from the logged on session
            // !!! this line will only work within the SYSTEM session !!!
            WTSQueryUserToken(WTSGetActiveConsoleSessionId(), out hToken);

            // Get the size required to host a SID
            GetTokenInformation(hToken, TokenInformationClass.TokenOwner, IntPtr.Zero, 0, out tokenSize);
            tokenOwnerPtr = Marshal.AllocHGlobal(tokenSize);

            // Get the SID structure within the TokenOwner class
            GetTokenInformation(hToken, TokenInformationClass.TokenOwner, tokenOwnerPtr, tokenSize, out tokenSize);
            TokenOwner tokenOwner = (TokenOwner)Marshal.PtrToStructure(tokenOwnerPtr, typeof(TokenOwner));

            // Convert the SID into a string
            string strSID = "";
            ConvertSidToStringSid(tokenOwner.Owner, ref strSID);
            Marshal.FreeHGlobal(tokenOwnerPtr);
            return strSID;
        }
    }
}
