using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AxMSTSCLib;
using MSTSCLib;

namespace SharpRDP
{
    public class Client
    {
        private Dictionary<string, Code> keycode;
        private IMsRdpClientNonScriptable keydata;
        private int LogonErrorCode { get; set; }
        private string cmd;
        private string execwith;
        private string target;
        private string language;
        private enum Logonerrors : uint
        {
            ARBITRATION_CODE_BUMP_OPTIONS = 0xFFFFFFFB,
            ARBITRATION_CODE_CONTINUE_LOGON = 0xFFFFFFFE,
            ARBITRATION_CODE_CONTINUE_TERMINATE = 0xFFFFFFFD,
            ARBITRATION_CODE_NOPERM_DIALOG = 0xFFFFFFFA,
            ARBITRATION_CODE_REFUSED_DIALOG = 0xFFFFFFF9,
            ARBITRATION_CODE_RECONN_OPTIONS = 0xFFFFFFFC,
            ERROR_CODE_ACCESS_DENIED = 0xFFFFFFFF,
            LOGON_FAILED_BAD_PASSWORD = 0x0,
            LOGON_FAILED_OTHER = 0x2,
            LOGON_FAILED_UPDATE_PASSWORD = 0x1,
            LOGON_WARNING = 0x3,
            STATUS_ACCOUNT_RESTRICTION = 0xC000006E,
            STATUS_LOGON_FAILURE = 0xC000006D,
            STATUS_PASSWORD_MUST_CHANGE = 0xC0000224
        }

        public void CreateRdpConnection(string server, string user, string domain, string password, string command, string execw, string language)
        {
            keycode = new Dictionary<String, Code>();
            KeyCodes(language);
            cmd = command;
            target = server;
            execwith = execw;

            void ProcessTaskThread()
            {
                var form = new Form();
                form.Opacity = 0;
                form.Visible = false;
                form.WindowState = FormWindowState.Minimized;
                form.ShowInTaskbar = false;
                form.FormBorderStyle = FormBorderStyle.None;
                form.Width = Screen.PrimaryScreen.WorkingArea.Width;
                form.Height = Screen.PrimaryScreen.WorkingArea.Height;
                form.Load += (sender, args) =>
                {
                    var rdpConnection = new AxMsRdpClient9NotSafeForScripting();
                    form.Controls.Add(rdpConnection);
                    var rdpC = rdpConnection.GetOcx() as IMsRdpClientNonScriptable5;
                    IMsRdpExtendedSettings rdpc2 = rdpConnection.GetOcx() as IMsRdpExtendedSettings;
                    rdpC.AllowPromptingForCredentials = false;
                    rdpC.AllowCredentialSaving = false;
                    rdpConnection.Server = server;
                    rdpConnection.Domain = domain;
                    rdpConnection.UserName = user;
                    rdpConnection.AdvancedSettings9.allowBackgroundInput = 1;
                    rdpConnection.AdvancedSettings9.BitmapPersistence = 0;
                    if (password != string.Empty || user != string.Empty)
                    {
                        rdpConnection.UserName = user;
                        rdpConnection.AdvancedSettings9.ClearTextPassword = password;
                    }
                    else
                    {
                        rdpc2.set_Property("RestrictedLogon", true);
                        rdpc2.set_Property("DisableCredentialsDelegation", true);
                    }
                    rdpConnection.AdvancedSettings9.EnableCredSspSupport = true;
                    if (true)
                    {
                        rdpConnection.OnDisconnected += RdpConnectionOnOnDisconnected;
                        rdpConnection.OnLoginComplete += RdpConnectionOnOnLoginComplete;
                        rdpConnection.OnLogonError += RdpConnectionOnOnLogonError;
                    }
                    rdpConnection.Connect();
                    rdpConnection.Enabled = false;
                    rdpConnection.Dock = DockStyle.Fill;
                    Application.Run(form);
                };
                form.Show();
            }

            var rdpClientThread = new Thread(ProcessTaskThread) { IsBackground = true };
            rdpClientThread.SetApartmentState(ApartmentState.STA);
            rdpClientThread.Start();
            while (rdpClientThread.IsAlive)
            {
                Task.Delay(500).GetAwaiter().GetResult();
            }
        }

        private void RdpConnectionOnOnLogonError(object sender, IMsTscAxEvents_OnLogonErrorEvent e)
        {
            LogonErrorCode = e.lError;
            var errorstatus = Enum.GetName(typeof(Logonerrors), (uint)LogonErrorCode);
            Console.WriteLine("[-] Logon Error    :  {0} - {1}", LogonErrorCode, errorstatus);
            Thread.Sleep(2000);
            if(LogonErrorCode != -2)
            {
                Environment.Exit(0);
            }
            
        }
        private void RdpConnectionOnOnLoginComplete(object sender, EventArgs e)
        {
            if (LogonErrorCode == -2)
            {
                Console.WriteLine($"[+] User not currently logged in, creating new session");
                Task.Delay(10000).GetAwaiter().GetResult();
            }
            var rdpSession = (AxMsRdpClient9NotSafeForScripting)sender;
            Console.WriteLine("[+] Connected to          :  {0}", target);
            Thread.Sleep(1000);
            keydata = (IMsRdpClientNonScriptable)rdpSession.GetOcx();
            string enterdown = "Enter+down";
            string enterup = "Enter+up";
            Thread.Sleep(1000);

            SendElement("Win+R+down");
            Thread.Sleep(500);
            SendElement("Win+R+up");
            Thread.Sleep(1000);

            if (execwith == "cmd")
            {
                Console.WriteLine("[+] Executing {0} from cmd.exe", cmd.ToLower());
                SendText("cmd.exe");
                Thread.Sleep(1000);

                SendElement(enterdown);
                Thread.Sleep(500);
                SendElement(enterup);

                Thread.Sleep(500);
                SendText(cmd.ToLower());

                Thread.Sleep(1000);

                SendElement(enterdown);
                Thread.Sleep(500);
                SendElement(enterup);

                Thread.Sleep(500);
                SendText("exit");
            }
            else if (execwith == "powershell" || execwith == "ps")
            {
                Console.WriteLine("[+] Executing {0} from powershell.exe", cmd.ToLower());
                SendText("powershell.exe");
                Thread.Sleep(1000);

                SendElement(enterdown);
                Thread.Sleep(500);
                SendElement(enterup);

                Thread.Sleep(500);
                SendText(cmd.ToLower());

                Thread.Sleep(1000);

                SendElement(enterdown);
                Thread.Sleep(500);
                SendElement(enterup);

                Thread.Sleep(500);
                SendText("exit");
            }
            else
            {
                Console.WriteLine("[+] Executing {0}", cmd.ToLower());
                SendText(cmd.ToLower());

                Thread.Sleep(1500);

                SendElement(enterdown);
                Thread.Sleep(500);
                SendElement(enterup);
            }

            Thread.Sleep(1000);
            Console.WriteLine("[+] Disconnecting from    :  {0}", target);
            rdpSession.Disconnect();
        }

        private void RdpConnectionOnOnDisconnected(object sender, IMsTscAxEvents_OnDisconnectedEvent e)
        {
            Console.WriteLine("[+] Connection closed     :  {0}", target);
            Environment.Exit(0);
        }

        public void SendText(String text)
        {
            foreach (var t in text)
            {
                var symbol = t.ToString();
                keydata.SendKeys(keycode[symbol].length, ref keycode[symbol].bools[0], ref keycode[symbol].ints[0]);
                Thread.Sleep(10);
            }
        }
        
        private void SendElement(String curchars)
        {
            var current = keycode[curchars];
            keydata.SendKeys(current.length, ref current.bools[0], ref current.ints[0]);
            Thread.Sleep(10);
        }

        private void KeyCodes(String language)
        {
            // See https://github.com/apache/guacamole-server/tree/master/src/protocols/rdp/keymaps to add more language
            // Other resource https://handmade.network/forums/t/2011-keyboard_inputs_-_scancodes,_raw_input,_text_input,_key_names
            // https://wiki.osdev.org/PS/2_Keyboard
            if (language.ToLower() == "fr")
            {
                keycode["Esc"] = new Code(new[] { false, true }, new[] { 0x01 });
                keycode["Enter+down"] = new Code(new[] { false }, new[] { 0x1c });
                keycode["Enter+up"] = new Code(new[] { true }, new[] { 0x1c });
                keycode["Win"] = new Code(new[] { false, true }, new[] { 0x15b });
                keycode["Down"] = new Code(new[] { false, true }, new[] { 0x150 });
                keycode["Right"] = new Code(new[] { false, true }, new[] { 0x14d });
                keycode["Left"] = new Code(new[] { false, true }, new[] { 0x14b });

                keycode["Calc"] = new Code(new[] { false, true }, new[] { 0x121, 0x121 });
                keycode["Paste"] = new Code(new[] { false, true }, new[] { 0x10a, 0x10a });

                keycode[" "] = new Code(new[] { false, true }, new[] { 0x39 });

                // azertyuiop$
                keycode["a"] = new Code(new[] { false, true }, new[] { 0x10 });
                keycode["z"] = new Code(new[] { false, true }, new[] { 0x11 });
                keycode["e"] = new Code(new[] { false, true }, new[] { 0x12 });
                keycode["r"] = new Code(new[] { false, true }, new[] { 0x13 });
                keycode["t"] = new Code(new[] { false, true }, new[] { 0x14 });
                keycode["y"] = new Code(new[] { false, true }, new[] { 0x15 });
                keycode["u"] = new Code(new[] { false, true }, new[] { 0x16 });
                keycode["i"] = new Code(new[] { false, true }, new[] { 0x17 });
                keycode["o"] = new Code(new[] { false, true }, new[] { 0x18 });
                keycode["p"] = new Code(new[] { false, true }, new[] { 0x19 });
                keycode["$"] = new Code(new[] { false, true }, new[] { 0x1b });

                // qsdfghjklmù*
                keycode["q"] = new Code(new[] { false, true }, new[] { 0x1e });
                keycode["s"] = new Code(new[] { false, true }, new[] { 0x1f });
                keycode["d"] = new Code(new[] { false, true }, new[] { 0x20 });
                keycode["f"] = new Code(new[] { false, true }, new[] { 0x21 });
                keycode["g"] = new Code(new[] { false, true }, new[] { 0x22 });
                keycode["h"] = new Code(new[] { false, true }, new[] { 0x23 });
                keycode["j"] = new Code(new[] { false, true }, new[] { 0x24 });
                keycode["k"] = new Code(new[] { false, true }, new[] { 0x25 });
                keycode["l"] = new Code(new[] { false, true }, new[] { 0x26 });
                keycode["m"] = new Code(new[] { false, true }, new[] { 0x27 });
                keycode["ù"] = new Code(new[] { false, true }, new[] { 0x28 });
                keycode["*"] = new Code(new[] { false, true }, new[] { 0x2b });

                // <wxcvbn,;:!
                keycode["<"] = new Code(new[] { false, true }, new[] { 0x56 });
                keycode["w"] = new Code(new[] { false, true }, new[] { 0x2c });
                keycode["x"] = new Code(new[] { false, true }, new[] { 0x2d });
                keycode["c"] = new Code(new[] { false, true }, new[] { 0x2e });
                keycode["v"] = new Code(new[] { false, true }, new[] { 0x2f });
                keycode["b"] = new Code(new[] { false, true }, new[] { 0x30 });          
                keycode["n"] = new Code(new[] { false, true }, new[] { 0x31 });
                keycode[","] = new Code(new[] { false, true }, new[] { 0x32 });
                keycode[";"] = new Code(new[] { false, true }, new[] { 0x33 });
                keycode[":"] = new Code(new[] { false, true }, new[] { 0x34 });
                keycode["!"] = new Code(new[] { false, true }, new[] { 0x35 });

                // ²&é"'(-è_çà)=
                keycode["²"] = new Code(new[] { false, true }, new[] { 0x29 });
                keycode["&"] = new Code(new[] { false, true }, new[] { 0x02 });
                keycode["é"] = new Code(new[] { false, true }, new[] { 0x03 });
                keycode["\""] = new Code(new[] { false, true }, new[] { 0x04 });
                keycode["'"] = new Code(new[] { false, true }, new[] { 0x05 });
                keycode["("] = new Code(new[] { false, true }, new[] { 0x06 });
                keycode["-"] = new Code(new[] { false, true }, new[] { 0x07 });
                keycode["è"] = new Code(new[] { false, true }, new[] { 0x08 });
                keycode["_"] = new Code(new[] { false, true }, new[] { 0x09 });
                keycode["ç"] = new Code(new[] { false, true }, new[] { 0x0a });
                keycode["à"] = new Code(new[] { false, true }, new[] { 0x0b });
                keycode[")"] = new Code(new[] { false, true }, new[] { 0x0c });
                keycode["="] = new Code(new[] { false, true }, new[] { 0x0d });

                // 1234567890°+
                keycode["1"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x02 });
                keycode["2"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x03 });
                keycode["3"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x04 });
                keycode["4"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x05 });
                keycode["5"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x06 });
                keycode["6"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x07 });
                keycode["7"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x08 });
                keycode["8"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x09 });
                keycode["9"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x0a });
                keycode["0"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x0b });
                keycode["°"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x0c });
                keycode["+"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x0d });

                // AZERTYUIOP£
                keycode["A"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x10 });
                keycode["Z"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x11 });
                keycode["E"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x12 });
                keycode["R"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x13 });
                keycode["T"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x14 });
                keycode["Y"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x15 });
                keycode["U"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x16 });
                keycode["I"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x17 });
                keycode["O"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x18 });
                keycode["P"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x19 });
                keycode["£"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x1b });

                // QSDFGHJKLM%µ
                keycode["Q"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x1e });
                keycode["S"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x1f });
                keycode["D"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x20 });
                keycode["F"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x21 });
                keycode["G"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x22 });
                keycode["H"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x23 });
                keycode["J"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x24 });
                keycode["K"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x25 });
                keycode["L"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x26 });
                keycode["M"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x27 });
                keycode["%"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x28 });
                keycode["µ"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x2b });

                // >WXCVBN?./§

                keycode[">"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x56 });
                keycode["W"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x2c });
                keycode["X"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x2d });
                keycode["C"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x2e });
                keycode["V"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x2f });
                keycode["B"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x30 });
                keycode["N"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x31 });
                keycode["?"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x32 });
                keycode["."] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x33 });
                keycode["/"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x34 });
                keycode["§"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x35 });

                // ~#{[|`\^@]}

                keycode["~"] = new Code(new[] { false, false, true, true }, new[] { 0x38, 0x03 });
                keycode["#"] = new Code(new[] { false, false, true, true }, new[] { 0x38, 0x04 });
                keycode["{"] = new Code(new[] { false, false, true, true }, new[] { 0x38, 0x05 });
                keycode["["] = new Code(new[] { false, false, true, true }, new[] { 0x38, 0x06 });
                keycode["|"] = new Code(new[] { false, false, true, true }, new[] { 0x38, 0x07 });
                keycode["`"] = new Code(new[] { false, false, true, true }, new[] { 0x38, 0x08 });
                keycode["\\"] = new Code(new[] { false, false, false, true, true, true }, new[] { 0x1d, 0x38, 0x09 });
                // keycode["\\"] = new Code(new[] { false, false, true, true }, new[] { 0xe038, 0x09 });
                keycode["^"] = new Code(new[] { false, false, true, true }, new[] { 0x38, 0x0a });
                keycode["@"] = new Code(new[] { false, false, true, true }, new[] { 0x38, 0x0b });
                keycode["]"] = new Code(new[] { false, false, true, true }, new[] { 0x38, 0x0c });
                keycode["}"] = new Code(new[] { false, false, true, true }, new[] { 0x38, 0x0d });


                keycode["Win+R+down"] = new Code(new[] { false, false }, new[] { 0x15b, 0x13 });
                keycode["Win+R+up"] = new Code(new[] { true, true }, new[] { 0x15b, 0x13 });
                keycode["Win+D"] = new Code(new[] { false, false, true, true }, new[] { 0x15b, 0x20 });
                keycode["Alt+Shift"] = new Code(new[] { false, false, true, true }, new[] { 0x38, 0x2a });
                keycode["Alt+Space"] = new Code(new[] { false, false, true, true }, new[] { 0x38, 0x39 });
                keycode["Ctrl+Shift"] = new Code(new[] { false, false, true, true }, new[] { 0x1d, 0x2a });
                keycode["Alt+F4"] = new Code(new[] { false, false, true, true }, new[] { 0x38, 0x3e });
                keycode["Ctrl+V"] = new Code(new[] { false, false, true, true }, new[] { 0x1d, 0x2f });
            }
            // default to "en"
            else
            {
                keycode["Esc"] = new Code(new[] { false, true }, new[] { 0x01 });
                keycode["Enter+down"] = new Code(new[] { false }, new[] { 0x1c });
                keycode["Enter+up"] = new Code(new[] { true }, new[] { 0x1c });
                keycode["Win"] = new Code(new[] { false, true }, new[] { 0x15b });
                keycode["Down"] = new Code(new[] { false, true }, new[] { 0x150 });
                keycode["Right"] = new Code(new[] { false, true }, new[] { 0x14d });
                keycode["Left"] = new Code(new[] { false, true }, new[] { 0x14b });

                keycode["Calc"] = new Code(new[] { false, true }, new[] { 0x121, 0x121 });
                keycode["Paste"] = new Code(new[] { false, true }, new[] { 0x10a, 0x10a });

                keycode["1"] = new Code(new[] { false, true }, new[] { 0x02 });
                keycode["2"] = new Code(new[] { false, true }, new[] { 0x03 });
                keycode["3"] = new Code(new[] { false, true }, new[] { 0x04 });
                keycode["4"] = new Code(new[] { false, true }, new[] { 0x05 });
                keycode["5"] = new Code(new[] { false, true }, new[] { 0x06 });
                keycode["6"] = new Code(new[] { false, true }, new[] { 0x07 });
                keycode["7"] = new Code(new[] { false, true }, new[] { 0x08 });
                keycode["8"] = new Code(new[] { false, true }, new[] { 0x09 });
                keycode["9"] = new Code(new[] { false, true }, new[] { 0x0a });
                keycode["0"] = new Code(new[] { false, true }, new[] { 0x0b });
                keycode["-"] = new Code(new[] { false, true }, new[] { 0x0c });

                keycode["a"] = new Code(new[] { false, true }, new[] { 0x1e });
                keycode["b"] = new Code(new[] { false, true }, new[] { 0x30 });
                keycode["c"] = new Code(new[] { false, true }, new[] { 0x2e });
                keycode["d"] = new Code(new[] { false, true }, new[] { 0x20 });
                keycode["e"] = new Code(new[] { false, true }, new[] { 0x12 });
                keycode["f"] = new Code(new[] { false, true }, new[] { 0x21 });
                keycode["g"] = new Code(new[] { false, true }, new[] { 0x22 });
                keycode["h"] = new Code(new[] { false, true }, new[] { 0x23 });
                keycode["i"] = new Code(new[] { false, true }, new[] { 0x17 });
                keycode["j"] = new Code(new[] { false, true }, new[] { 0x24 });
                keycode["k"] = new Code(new[] { false, true }, new[] { 0x25 });
                keycode["l"] = new Code(new[] { false, true }, new[] { 0x26 });
                keycode["m"] = new Code(new[] { false, true }, new[] { 0x32 });
                keycode["n"] = new Code(new[] { false, true }, new[] { 0x31 });
                keycode["o"] = new Code(new[] { false, true }, new[] { 0x18 });
                keycode["p"] = new Code(new[] { false, true }, new[] { 0x19 });
                keycode["q"] = new Code(new[] { false, true }, new[] { 0x10 });
                keycode["r"] = new Code(new[] { false, true }, new[] { 0x13 });
                keycode["s"] = new Code(new[] { false, true }, new[] { 0x1f });
                keycode["t"] = new Code(new[] { false, true }, new[] { 0x14 });
                keycode["u"] = new Code(new[] { false, true }, new[] { 0x16 });
                keycode["v"] = new Code(new[] { false, true }, new[] { 0x2f });
                keycode["w"] = new Code(new[] { false, true }, new[] { 0x11 });
                keycode["x"] = new Code(new[] { false, true }, new[] { 0x2d });
                keycode["y"] = new Code(new[] { false, true }, new[] { 0x15 });
                keycode["z"] = new Code(new[] { false, true }, new[] { 0x2c });
                keycode[" "] = new Code(new[] { false, true }, new[] { 0x39 });

                keycode[","] = new Code(new[] { false, true }, new[] { 0x33 });
                keycode["."] = new Code(new[] { false, true }, new[] { 0x34 });
                keycode["/"] = new Code(new[] { false, true }, new[] { 0x35 });
                keycode["["] = new Code(new[] { false, true }, new[] { 0x1a });
                keycode["]"] = new Code(new[] { false, true }, new[] { 0x1b });
                keycode["\\"] = new Code(new[] { false, true }, new[] { 0x2b });
                keycode[";"] = new Code(new[] { false, true }, new[] { 0x27 });
                keycode["'"] = new Code(new[] { false, true }, new[] { 0x28 });

                keycode["\""] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x28 });
                keycode[":"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x27 });
                keycode["|"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x2b });
                keycode["&"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x08 });
                keycode["%"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x06 });
                keycode["("] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x0a });
                keycode[")"] = new Code(new[] { false, false, true, true }, new[] { 0x2a, 0x0b });

                keycode["Win+R+down"] = new Code(new[] { false, false }, new[] { 0x15b, 0x13 });
                keycode["Win+R+up"] = new Code(new[] { true, true }, new[] { 0x15b, 0x13 });
                keycode["Win+D"] = new Code(new[] { false, false, true, true }, new[] { 0x15b, 0x20 });
                keycode["Alt+Shift"] = new Code(new[] { false, false, true, true }, new[] { 0x38, 0x2a });
                keycode["Alt+Space"] = new Code(new[] { false, false, true, true }, new[] { 0x38, 0x39 });
                keycode["Ctrl+Shift"] = new Code(new[] { false, false, true, true }, new[] { 0x1d, 0x2a });
                keycode["Alt+F4"] = new Code(new[] { false, false, true, true }, new[] { 0x38, 0x3e });
                keycode["Ctrl+V"] = new Code(new[] { false, false, true, true }, new[] { 0x1d, 0x2f });
            }
        }
    }
}
