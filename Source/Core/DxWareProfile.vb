' KOCH SpaceMouse - writes the 12 LCD keys into 3DxWare's profile for Alibre Design
' (c) 2026 KOCH Engineering - MIT License
'
' 3DxWare keeps one XML file per application in %APPDATA%\3Dconnexion\3DxWare\Cfg.
' Alibre Design connects as "Alibre Design 3DXware V2". In that profile:
'   ButtonActions/ButtonAction  - a named macro with an icon (PNG, base64) and a key sequence
'   MacroTable/MacroEntry       - a key stroke (HID usage codes, hex)
'   Devices/Device[ID_ProductID_C633]/ButtonBank - maps HIDButton_13..22, 77, 78 (LCD keys 1..12) to macros
' Everything this add-on writes has an ID starting with "KOCH_", so the user's own entries are kept.

Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Text
Imports System.Xml
Imports System.Xml.Linq

Namespace KochSpaceMouse

    Public Class DxWareProfile

        Public Const SiOpenAppName As String = "Alibre Design 3DXware V2"
        Public Const DeviceId As String = "ID_ProductID_C633"   ' SpaceMouse Enterprise
        Public Const KeyboardId As String = "ID_ProductID_C668" ' 3Dconnexion Keyboard Pro
        Public Const FirstLcdButton As Integer = 13              ' HIDButton_13 = LCD key 1

        ' LCD keys 1-10 are HIDButton_13..22, keys 11 and 12 are HIDButton_77 and 78 (from 3Dconnexion's Base.xml;
        ' HIDButton_23/24 are the Esc and Alt keys of the Enterprise)
        Public Shared Function SpaceMouseInput(i As Integer) As String
            If i < 10 Then Return "HIDButton_" & (FirstLcdButton + i)
            Return "HIDButton_" & (77 + i - 10)
        End Function

        Public Shared Function KeyboardInput(i As Integer) As String
            Return "V3DX_F" & (i + 1)                               ' Keyboard Pro keys 1..12
        End Function

        ''' <summary>Which 3Dconnexion devices 3DxWare knows on this PC (from Global.xml).</summary>
        Public Shared Sub DetectDevices(ByRef spaceMouse As Boolean, ByRef keyboard As Boolean)
            spaceMouse = False
            keyboard = False
            Try
                Dim g = Path.Combine(CfgFolder, "Global.xml")
                If IO.File.Exists(g) Then
                    Dim txt = IO.File.ReadAllText(g)
                    spaceMouse = txt.Contains(DeviceId)
                    keyboard = txt.Contains(KeyboardId)
                End If
                If Not keyboard Then
                    Dim sh = Path.Combine(CfgFolder, "SharedCfg.xml")
                    If IO.File.Exists(sh) Then keyboard = IO.File.ReadAllText(sh).Contains(KeyboardId)
                End If
            Catch
            End Try
        End Sub
        Private Const Prefix As String = "KOCH_"

        Public Shared ReadOnly Property CfgFolder As String
            Get
                Return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                    "3Dconnexion", "3DxWare", "Cfg")
            End Get
        End Property

        ''' <summary>The profile file 3DxWare uses for Alibre Design (created if missing).</summary>
        Public Shared Function FindProfile() As String
            If Directory.Exists(CfgFolder) Then
                For Each f In Directory.GetFiles(CfgFolder, "*.xml")
                    Try
                        Dim txt = File.ReadAllText(f)
                        If txt.IndexOf("<SiOpenAppName>" & SiOpenAppName & "</SiOpenAppName>", StringComparison.OrdinalIgnoreCase) >= 0 Then Return f
                    Catch
                    End Try
                Next
            End If
            Return Path.Combine(CfgFolder, "Alibre Design V2.xml")
        End Function

        ''' <summary>Writes the 12 keys and the radial menu. functions(i) Nothing = leave key i+1 to 3DxWare's default.
        ''' radial all Nothing = the MENU button keeps 3DxWare's own menu.</summary>
        Public Shared Function Write(functions As KeyFunction(), keyboardFunctions As KeyFunction(), radial As KeyFunction(),
                                     context As String, iconFor As Func(Of KeyFunction, Byte())) As String
            Dim file = FindProfile()
            Dim doc As XDocument
            If IO.File.Exists(file) Then
                Backup(file)
                doc = XDocument.Load(file, LoadOptions.None)
            Else
                Directory.CreateDirectory(CfgFolder)
                doc = NewProfile()
            End If
            Dim root = doc.Root

            Dim actions = GetOrAdd(root, "ButtonActions")
            Dim macros = GetOrAdd(root, "MacroTable")
            actions.Elements("ButtonAction").Where(Function(e) IdOf(e).StartsWith(Prefix)).ToList().ForEach(Sub(e) e.Remove())
            macros.Elements("MacroEntry").Where(Function(e) IdOf(e).StartsWith(Prefix)).ToList().ForEach(Sub(e) e.Remove())

            Dim written As New HashSet(Of String)
            WriteDevice(root, DeviceId, AddressOf SpaceMouseInput, functions, actions, macros, written, iconFor)
            If keyboardFunctions IsNot Nothing Then
                WriteDevice(root, KeyboardId, AddressOf KeyboardInput, keyboardFunctions, actions, macros, written, iconFor)
            End If
            WriteRadial(root, radial, context, actions, macros, written, iconFor)

            Dim ws As New XmlWriterSettings With {.Encoding = New UTF8Encoding(False), .Indent = True}
            Dim tmp = file & ".koch.tmp"
            Using w = XmlWriter.Create(tmp, ws)
                doc.Save(w)
            End Using
            If IO.File.Exists(file) Then IO.File.Delete(file)
            IO.File.Move(tmp, file)
            Return file
        End Function

        Private Shared Sub WriteDevice(root As XElement, device As String, inputFor As Func(Of Integer, String), functions As KeyFunction(),
                                       actions As XElement, macros As XElement, written As HashSet(Of String),
                                       iconFor As Func(Of KeyFunction, Byte()))
            Dim bank = GetBank(root, device)
            Dim inputs As New HashSet(Of String)
            For i = 0 To Contexts.KeyCount - 1
                inputs.Add(inputFor(i))
            Next
            ' remove our old entries (also ones on wrong inputs from earlier versions) and anything on the keys we set
            bank.Elements("Button").Where(Function(b) inputs.Contains(InputOf(b)) OrElse OutputOf(b).StartsWith(Prefix)).ToList().ForEach(Sub(b) b.Remove())

            For i = 0 To Contexts.KeyCount - 1
                Dim f = functions(i)
                If f Is Nothing Then Continue For
                Dim actionId = AddAction(f, actions, macros, written, iconFor)
                bank.Add(New XElement("Button",
                    New XElement("Input", New XElement("ActionID", inputFor(i))),
                    New XElement("Output", New XElement("ActionID", actionId))))
            Next
        End Sub

        ''' <summary>Adds the ButtonAction (and its key stroke) for a function once; returns its action id.</summary>
        Private Shared Function AddAction(f As KeyFunction, actions As XElement, macros As XElement, written As HashSet(Of String),
                                          iconFor As Func(Of KeyFunction, Byte())) As String
            Dim actionId = Prefix & f.Id
            If written.Contains(actionId) Then Return actionId
            written.Add(actionId)
            Dim seq As New XElement("Sequence", New XAttribute("Pause", "120"))
            If Not String.IsNullOrWhiteSpace(f.Shortcut) Then
                Dim macroId = Prefix & f.Shortcut.ToUpperInvariant()
                If Not macros.Elements("MacroEntry").Any(Function(e) IdOf(e) = macroId) Then
                    macros.Add(KeyStroke.ToMacroEntry(macroId, f.Shortcut))
                End If
                seq.Add(New XElement("ActionID", New XAttribute("Pause", "40"), macroId))
            End If
            Dim action As New XElement("ButtonAction", New XAttribute("Type", "Sequence"),
                New XElement("ID", actionId),
                New XElement("Name", f.Name))
            Dim png = iconFor(f)
            If png IsNot Nothing AndAlso png.Length > 0 Then
                action.Add(New XElement("Image", New XAttribute("Type", "Data"),
                    New XElement("Data", New XAttribute("encoding", "base64"), Convert.ToBase64String(png))))
            End If
            If Not seq.HasElements Then seq.Value = ""   ' write <Sequence ...></Sequence> like 3DxWare does
            action.Add(seq)
            actions.Add(action)
            Return actionId
        End Function

        ' ---------------------------------------------------------------- radial menu
        '
        ' 3DxWare draws radial menus with 3DxPieMenus.exe. In the profile:
        '   Menus/Menu[@Type=Radial]   - ID, Name, Style (EightButton), Button: MenuButton_1..8 -> ButtonAction
        '   ButtonAction Type="Menu"   - opens a menu (MenuID); ID "RadialMenu_" + menu id
        '   Device ID_Standard_3D_Mouse - virtual keys shared by every SpaceMouse; V3DK_MENU is the MENU button
        '                                (left button on the SpaceMouse Compact, MENU key on Pro/Enterprise)

        Public Const StandardMouseId As String = "ID_Standard_3D_Mouse"
        Public Const MenuInput As String = "V3DK_MENU"
        Private Const RadialMenuId As String = "KOCH_MenuRM_Alibre"
        Private Const RadialActionId As String = "KOCH_RadialMenu_Alibre"

        Private Shared Sub WriteRadial(root As XElement, radial As KeyFunction(), context As String, actions As XElement, macros As XElement,
                                       written As HashSet(Of String), iconFor As Func(Of KeyFunction, Byte()))
            ' remove what we wrote before
            Dim menus = root.Element("Menus")
            If menus IsNot Nothing Then
                menus.Elements("Menu").Where(Function(m) IdOf(m).StartsWith(Prefix)).ToList().ForEach(Sub(m) m.Remove())
            End If
            Dim bank = GetBank(root, StandardMouseId)
            bank.Elements("Button").Where(Function(b) InputOf(b) = MenuInput AndAlso OutputOf(b).StartsWith(Prefix)).ToList().ForEach(Sub(b) b.Remove())

            If radial Is Nothing OrElse radial.All(Function(f) f Is Nothing) Then
                ' nothing chosen: leave the MENU button to 3DxWare
                If menus IsNot Nothing AndAlso Not menus.Elements("Menu").Any() Then menus.Remove()
                If Not bank.Elements("Button").Any() Then
                    Dim dev = bank.Parent
                    bank.Remove()
                    If dev IsNot Nothing AndAlso dev.Elements().All(Function(x) x.Name = "ID") Then dev.Remove()
                End If
                Return
            End If

            If menus Is Nothing Then
                menus = New XElement("Menus", New XElement("Executable", "3DxPieMenus.exe"))
                Dim before = root.Element("Devices")
                If before IsNot Nothing Then before.AddBeforeSelf(menus) Else root.Add(menus)
            ElseIf menus.Element("Executable") Is Nothing Then
                menus.AddFirst(New XElement("Executable", "3DxPieMenus.exe"))
            End If

            Dim menuName = "Alibre " & If(String.IsNullOrEmpty(context), "", context)
            Dim menu As New XElement("Menu", New XAttribute("Type", "Radial"),
                New XElement("ID", RadialMenuId),
                New XElement("Name", menuName.Trim()),
                New XElement("GesturesEnabled", "true"),
                New XElement("Style", "EightButton"))
            For i = 0 To Math.Min(radial.Length, Contexts.RadialSlots) - 1
                Dim f = radial(i)
                If f Is Nothing Then Continue For
                Dim actionId = AddAction(f, actions, macros, written, iconFor)
                menu.Add(New XElement("Button",
                    New XElement("Input", New XElement("ActionID", "MenuButton_" & (i + 1))),
                    New XElement("Output", New XElement("ActionID", actionId))))
            Next
            menus.Add(menu)

            ' the ButtonAction that opens the menu (same form as 3DxWare writes it)
            Dim xsi As XNamespace = "http://www.w3.org/2001/XMLSchema-instance"
            If root.Attribute(XNamespace.Xmlns + "xsi") Is Nothing Then root.Add(New XAttribute(XNamespace.Xmlns + "xsi", xsi.NamespaceName))
            actions.Add(New XElement("ButtonAction", New XAttribute("Type", "Menu"), New XAttribute(xsi + "type", "ButtonAction_Menu"),
                New XElement("ID", RadialActionId),
                New XElement("Name", menuName.Trim()),
                New XElement("MenuID", RadialMenuId)))

            ' MENU button -> our menu (a button the user mapped there himself is replaced; 3DxWare's default comes back when the menu is emptied)
            bank.Elements("Button").Where(Function(b) InputOf(b) = MenuInput).ToList().ForEach(Sub(b) b.Remove())
            bank.Add(New XElement("Button",
                New XElement("Input", New XElement("ActionID", MenuInput)),
                New XElement("Output", New XElement("ActionID", RadialActionId))))
        End Sub

        Private Shared Function OutputOf(button As XElement) As String
            Dim o = button.Element("Output")
            If o Is Nothing Then Return ""
            Dim a = o.Element("ActionID")
            Return If(a Is Nothing, "", a.Value.Trim())
        End Function

        ' ---------------------------------------------------------------- helpers

        Private Shared Sub Backup(file As String)
            Try
                Dim bak = Path.Combine(Path.GetDirectoryName(file), "bak")
                Directory.CreateDirectory(bak)
                Dim name = Path.GetFileNameWithoutExtension(file)
                IO.File.Copy(file, Path.Combine(bak, name & ".koch-" & DateTime.Now.ToString("yyyyMMdd-HHmmss") & ".bak"), True)
                ' keep the 10 newest backups made by this add-on
                Dim old = New DirectoryInfo(bak).GetFiles(name & ".koch-*.bak").OrderByDescending(Function(x) x.Name).Skip(10)
                For Each o In old
                    o.Delete()
                Next
            Catch
            End Try
        End Sub

        Private Shared Function NewProfile() As XDocument
            Return New XDocument(
                New XElement("AppCfg",
                    New XAttribute("Default", "false"), New XAttribute("CfgFormatVersion", "1.3"), New XAttribute("ThisFileVersion", "1.0"),
                    New XElement("AppInfo",
                        New XElement("Signature", New XElement("Name", "Alibre Design"), New XElement("SiOpenAppName", SiOpenAppName)),
                        New XElement("Options", New XElement("UseSiOpenAppName", "true"))),
                    New XElement("CfgProperties",
                        New XElement("ID", "ID_AlibreDesign"), New XElement("Name", "Alibre Design"), New XElement("InheritsFromID", "ID_AlibreDesign")),
                    New XElement("ButtonActions"),
                    New XElement("Devices"),
                    New XElement("MacroTable")))
        End Function

        Private Shared Function GetOrAdd(parent As XElement, name As String) As XElement
            Dim e = parent.Element(name)
            If e Is Nothing Then
                e = New XElement(name)
                parent.Add(e)
            End If
            Return e
        End Function

        Private Shared Function GetBank(root As XElement, device As String) As XElement
            Dim devices = GetOrAdd(root, "Devices")
            Dim dev = devices.Elements("Device").FirstOrDefault(Function(d) IdOf(d) = device)
            If dev Is Nothing Then
                dev = New XElement("Device", New XElement("ID", device))
                devices.AddFirst(dev)
            End If
            Dim bank = dev.Elements("ButtonBank").FirstOrDefault(Function(b) CStr(b.Attribute("Default")) = "true" OrElse IdOf(b) = "Default")
            If bank Is Nothing Then
                bank = New XElement("ButtonBank", New XAttribute("Default", "true"),
                                    New XElement("Name", "STR_DEFAULT_BUTTONBANK"), New XElement("ID", "Default"))
                dev.Element("ID").AddAfterSelf(bank)
            End If
            Return bank
        End Function

        Private Shared Function IdOf(e As XElement) As String
            Dim id = e.Element("ID")
            Return If(id Is Nothing, "", id.Value.Trim())
        End Function

        Private Shared Function InputOf(button As XElement) As String
            Dim inp = button.Element("Input")
            If inp Is Nothing Then Return ""
            Dim a = inp.Element("ActionID")
            Return If(a Is Nothing, "", a.Value.Trim())
        End Function
    End Class

    ''' <summary>Keyboard shortcuts as text ("CTRL+ALT+E") and as 3DxWare HID key strokes.</summary>
    Public Module KeyStroke

        Private ReadOnly Named As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase) From {
            {"ENTER", &H28}, {"ESC", &H29}, {"BACKSPACE", &H2A}, {"TAB", &H2B}, {"SPACE", &H2C},
            {"-", &H2D}, {"=", &H2E}, {"[", &H2F}, {"]", &H30}, {"\", &H31}, {";", &H33}, {"'", &H34},
            {"`", &H35}, {",", &H36}, {".", &H37}, {"/", &H38},
            {"INSERT", &H49}, {"HOME", &H4A}, {"PGUP", &H4B}, {"DELETE", &H4C}, {"END", &H4D}, {"PGDN", &H4E},
            {"RIGHT", &H4F}, {"LEFT", &H50}, {"DOWN", &H51}, {"UP", &H52}}

        Public Function Parse(shortcut As String, ByRef mods As List(Of String), ByRef usage As Integer) As Boolean
            mods = New List(Of String)
            usage = -1
            If String.IsNullOrWhiteSpace(shortcut) Then Return False
            Dim parts = shortcut.ToUpperInvariant().Split("+"c)
            ' "CTRL++" -> the last key is "+"
            For i = 0 To parts.Length - 1
                Dim p = parts(i).Trim()
                Select Case p
                    Case "CTRL", "CONTROL" : mods.Add("Control")
                    Case "ALT" : mods.Add("Alt")
                    Case "SHIFT" : mods.Add("Shift")
                    Case "WIN" : mods.Add("Win")
                    Case "" : Continue For
                    Case Else
                        If i <> parts.Length - 1 Then Return False
                        usage = KeyUsage(p)
                End Select
            Next
            Return usage >= 0
        End Function

        Public Function KeyUsage(key As String) As Integer
            If key.Length = 1 AndAlso key(0) >= "A"c AndAlso key(0) <= "Z"c Then Return &H4 + (AscW(key(0)) - AscW("A"c))
            If key.Length = 1 AndAlso key(0) >= "1"c AndAlso key(0) <= "9"c Then Return &H1E + (AscW(key(0)) - AscW("1"c))
            If key = "0" Then Return &H27
            If key.Length >= 2 AndAlso key(0) = "F"c Then
                Dim n As Integer
                If Integer.TryParse(key.Substring(1), n) AndAlso n >= 1 AndAlso n <= 12 Then Return &H3A + n - 1
                If n >= 13 AndAlso n <= 24 Then Return &H68 + n - 13        ' F13-F24: invisible keys run by the add-on
            End If
            Dim v As Integer
            If Named.TryGetValue(key, v) Then Return v
            Return -1
        End Function

        Public Function ToMacroEntry(id As String, shortcut As String) As XElement
            Dim mods As List(Of String) = Nothing
            Dim usage As Integer
            If Not Parse(shortcut, mods, usage) Then Throw New ArgumentException("Unknown shortcut: " & shortcut)
            Dim ks As New XElement("KeyStroke")
            If mods.Count > 0 Then
                ks.Add(New XElement("Modifiers", mods.Select(Function(m) New XElement("Modifier", m))))
            End If
            ks.Add(New XElement("Key", usage.ToString("X")))
            Return New XElement("MacroEntry", New XElement("ID", id), ks)
        End Function

        ''' <summary>Turns a WinForms key press into the text form, e.g. "CTRL+ALT+E".</summary>
        Public Function FromKeys(k As System.Windows.Forms.Keys) As String
            Dim code = k And System.Windows.Forms.Keys.KeyCode
            Select Case code
                Case System.Windows.Forms.Keys.ControlKey, System.Windows.Forms.Keys.ShiftKey,
                     System.Windows.Forms.Keys.Menu, System.Windows.Forms.Keys.LWin, System.Windows.Forms.Keys.RWin
                    Return ""   ' a modifier alone
            End Select
            Dim name As String = Nothing
            Select Case code
                Case System.Windows.Forms.Keys.Return : name = "ENTER"
                Case System.Windows.Forms.Keys.Escape : name = "ESC"
                Case System.Windows.Forms.Keys.Back : name = "BACKSPACE"
                Case System.Windows.Forms.Keys.Tab : name = "TAB"
                Case System.Windows.Forms.Keys.Space : name = "SPACE"
                Case System.Windows.Forms.Keys.Insert : name = "INSERT"
                Case System.Windows.Forms.Keys.Delete : name = "DELETE"
                Case System.Windows.Forms.Keys.Home : name = "HOME"
                Case System.Windows.Forms.Keys.End : name = "END"
                Case System.Windows.Forms.Keys.PageUp : name = "PGUP"
                Case System.Windows.Forms.Keys.PageDown : name = "PGDN"
                Case System.Windows.Forms.Keys.Left : name = "LEFT"
                Case System.Windows.Forms.Keys.Right : name = "RIGHT"
                Case System.Windows.Forms.Keys.Up : name = "UP"
                Case System.Windows.Forms.Keys.Down : name = "DOWN"
                Case System.Windows.Forms.Keys.OemMinus : name = "-"
                Case System.Windows.Forms.Keys.Oemplus : name = "="
                Case System.Windows.Forms.Keys.Oemcomma : name = ","
                Case System.Windows.Forms.Keys.OemPeriod : name = "."
                Case Else
                    If code >= System.Windows.Forms.Keys.A AndAlso code <= System.Windows.Forms.Keys.Z Then
                        name = ChrW(AscW("A"c) + (code - System.Windows.Forms.Keys.A))
                    ElseIf code >= System.Windows.Forms.Keys.D0 AndAlso code <= System.Windows.Forms.Keys.D9 Then
                        name = ChrW(AscW("0"c) + (code - System.Windows.Forms.Keys.D0))
                    ElseIf code >= System.Windows.Forms.Keys.F1 AndAlso code <= System.Windows.Forms.Keys.F12 Then
                        name = "F" & (code - System.Windows.Forms.Keys.F1 + 1)
                    End If
            End Select
            If name Is Nothing Then Return ""
            Dim sb As New StringBuilder()
            If (k And System.Windows.Forms.Keys.Control) <> 0 Then sb.Append("CTRL+")
            If (k And System.Windows.Forms.Keys.Alt) <> 0 Then sb.Append("ALT+")
            If (k And System.Windows.Forms.Keys.Shift) <> 0 Then sb.Append("SHIFT+")
            sb.Append(name)
            Return sb.ToString()
        End Function
    End Module

End Namespace
