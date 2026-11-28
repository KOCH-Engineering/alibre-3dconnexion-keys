' KOCH SpaceMouse - data model
' (c) 2026 KOCH Engineering - MIT License

Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Xml.Linq

Namespace KochSpaceMouse

    ''' <summary>A function that can be put on a SpaceMouse key.</summary>
    Public Class KeyFunction
        Public Property Id As String = ""
        Public Property Name As String = ""
        ''' <summary>Keyboard shortcut, e.g. "CTRL+ALT+E". Empty = no shortcut yet.</summary>
        Public Property Shortcut As String = ""
        ''' <summary>Part, Assembly, Sketch, Drawing or General - used for the icon colour.</summary>
        Public Property Category As String = "General"
        ''' <summary>PNG file name in the user's icon folder. Empty = generated icon.</summary>
        Public Property Icon As String = ""

        Public Overrides Function ToString() As String
            Return Name
        End Function
    End Class

    Public Module Contexts
        Public ReadOnly All As String() = {"Part", "Assembly", "Sketch", "Drawing"}
        Public Const KeyCount As Integer = 12
        Public Const KeyboardSuffix As String = "|Keyboard"
        ''' <summary>Radial menu on the MENU button of any SpaceMouse (Compact, Wireless, Pro, Enterprise).</summary>
        Public Const RadialSuffix As String = "|Radial"
        Public Const RadialSlots As Integer = 8

        ''' <summary>Layout keys: "Part" ... for the SpaceMouse, "Part|Keyboard" ... for the Keyboard Pro
        ''' and "Part|Radial" ... for the radial menu (12 slots stored, the first 8 used).</summary>
        Public Function AllLayoutKeys() As String()
            Return All.Concat(All.Select(Function(c) c & KeyboardSuffix)).Concat(All.Select(Function(c) c & RadialSuffix)).ToArray()
        End Function
    End Module

    ''' <summary>The user's catalog of functions and the 12-key layout for each context.</summary>
    Public Class Settings
        Public Property Functions As New List(Of KeyFunction)
        ''' <summary>Context -> 12 function ids ("" = 3DxWare default for that key).</summary>
        Public Property Layouts As New Dictionary(Of String, String())
        Public Property AutoSwitch As Boolean = True

        Public Shared ReadOnly Property Folder As String
            Get
                Return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                    "Koch Engineering", "SpaceMouse")
            End Get
        End Property

        Public Shared ReadOnly Property IconFolder As String
            Get
                Return Path.Combine(Folder, "icons")
            End Get
        End Property

        Public Shared ReadOnly Property FilePath As String
            Get
                Return Path.Combine(Folder, "settings.xml")
            End Get
        End Property

        Public Function Find(id As String) As KeyFunction
            If String.IsNullOrEmpty(id) Then Return Nothing
            Return Functions.FirstOrDefault(Function(f) f.Id = id)
        End Function

        Public Function Layout(context As String) As String()
            Dim l As String() = Nothing
            If Not Layouts.TryGetValue(context, l) OrElse l Is Nothing OrElse l.Length <> KeyCount Then
                l = Enumerable.Repeat("", KeyCount).ToArray()
                Layouts(context) = l
            End If
            Return l
        End Function

        Public Function NewId() As String
            Return "f_" & Guid.NewGuid().ToString("N").Substring(0, 10)
        End Function

        ' ---------------------------------------------------------------- load / save

        ''' <summary>Loads the user's settings; on first run copies defaults (and icons) from the add-on folder.</summary>
        Public Shared Function Load(installFolder As String) As Settings
            Directory.CreateDirectory(IconFolder)
            If Not File.Exists(FilePath) Then
                Dim def = Path.Combine(installFolder, "defaults", "settings.xml")
                If File.Exists(def) Then File.Copy(def, FilePath)
                Dim defIcons = Path.Combine(installFolder, "defaults", "icons")
                If Directory.Exists(defIcons) Then
                    For Each f In Directory.GetFiles(defIcons, "*.png")
                        Dim target = Path.Combine(IconFolder, Path.GetFileName(f))
                        If Not File.Exists(target) Then File.Copy(f, target)
                    Next
                End If
            End If
            If File.Exists(FilePath) Then Return FromXml(XDocument.Load(FilePath))
            Return New Settings()
        End Function

        Public Sub Save()
            Directory.CreateDirectory(Folder)
            Dim tmp = FilePath & ".tmp"
            ToXml().Save(tmp)
            If File.Exists(FilePath) Then File.Delete(FilePath)
            File.Move(tmp, FilePath)
        End Sub

        Public Function ToXml() As XDocument
            Dim root As New XElement("SpaceMouse",
                New XAttribute("version", "1"),
                New XElement("AutoSwitch", AutoSwitch))
            Dim fx As New XElement("Functions")
            For Each f In Functions
                fx.Add(New XElement("Function",
                    New XAttribute("id", f.Id),
                    New XAttribute("name", f.Name),
                    New XAttribute("shortcut", f.Shortcut),
                    New XAttribute("category", f.Category),
                    New XAttribute("icon", f.Icon)))
            Next
            root.Add(fx)
            Dim lx As New XElement("Layouts")
            For Each c In Contexts.AllLayoutKeys()
                Dim l = Layout(c)
                Dim e As New XElement("Layout", New XAttribute("context", c))
                For i = 0 To KeyCount - 1
                    e.Add(New XElement("Key", New XAttribute("n", i + 1), New XAttribute("function", l(i))))
                Next
                lx.Add(e)
            Next
            root.Add(lx)
            Return New XDocument(root)
        End Function

        Public Shared Function FromXml(doc As XDocument) As Settings
            Dim s As New Settings()
            Dim root = doc.Root
            Dim a = root.Element("AutoSwitch")
            If a IsNot Nothing Then s.AutoSwitch = (a.Value.Trim().ToLowerInvariant() = "true")
            Dim fx = root.Element("Functions")
            If fx IsNot Nothing Then
                For Each e In fx.Elements("Function")
                    s.Functions.Add(New KeyFunction With {
                        .Id = Attr(e, "id"), .Name = Attr(e, "name"), .Shortcut = Attr(e, "shortcut"),
                        .Category = If(Attr(e, "category") = "", "General", Attr(e, "category")),
                        .Icon = Attr(e, "icon")})
                Next
            End If
            Dim lx = root.Element("Layouts")
            If lx IsNot Nothing Then
                For Each e In lx.Elements("Layout")
                    Dim ctx = Attr(e, "context")
                    If Not Contexts.AllLayoutKeys().Contains(ctx) Then Continue For
                    Dim l = s.Layout(ctx)
                    For Each k In e.Elements("Key")
                        Dim n As Integer
                        If Integer.TryParse(Attr(k, "n"), n) AndAlso n >= 1 AndAlso n <= KeyCount Then
                            l(n - 1) = Attr(k, "function")
                        End If
                    Next
                Next
            End If
            For Each c In Contexts.AllLayoutKeys()
                s.Layout(c)
            Next
            Return s
        End Function

        Private Shared Function Attr(e As XElement, name As String) As String
            Dim a = e.Attribute(name)
            Return If(a Is Nothing, "", a.Value)
        End Function

        ''' <summary>Exports just the layouts plus the functions they use (for sharing).</summary>
        Public Sub ExportLayout(file As String)
            ToXml().Save(file)
        End Sub

        ''' <summary>Imports a shared file: functions are merged by name, layouts replaced.</summary>
        Public Sub ImportLayout(file As String)
            Dim other = FromXml(XDocument.Load(file))
            Dim map As New Dictionary(Of String, String)
            For Each f In other.Functions
                Dim mine = Functions.FirstOrDefault(Function(x) String.Equals(x.Name, f.Name, StringComparison.OrdinalIgnoreCase))
                If mine Is Nothing Then
                    mine = New KeyFunction With {.Id = NewId(), .Name = f.Name, .Shortcut = f.Shortcut,
                                                 .Category = f.Category, .Icon = f.Icon}
                    Functions.Add(mine)
                End If
                map(f.Id) = mine.Id
            Next
            For Each c In Contexts.AllLayoutKeys()
                Dim src = other.Layout(c)
                Dim dst = Layout(c)
                For i = 0 To KeyCount - 1
                    Dim id = ""
                    If src(i) <> "" AndAlso map.TryGetValue(src(i), id) Then dst(i) = id Else dst(i) = ""
                Next
            Next
        End Sub
    End Class

End Namespace
