' KOCH SpaceMouse - read Alibre's own commands (caption, shortcut, icon) from its ribbon at run time
' (c) 2026 KOCH Engineering - MIT License
'
' Alibre Design uses DevExpress ribbons. Every command is a BarItem with a caption, an optional
' ItemShortcut and an image (raster or SVG). The add-on runs inside Alibre, so it can look at the
' open windows and read them. Reflection is used, so no DevExpress reference is needed.

Option Strict Off

Imports System.Collections
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Reflection
Imports System.Windows.Forms

Namespace KochSpaceMouse

    Public Class FoundCommand
        Public Property Id As String = ""
        Public Property Caption As String = ""
        Public Property Shortcut As String = ""
        Public Property Page As String = ""
        Public Property Window As String = ""
        ''' <summary>Ribbon items for this command; the icon is rendered only when first needed.</summary>
        Friend ReadOnly Items As New List(Of Object)
        Private _image As Bitmap
        Private _imageDone As Boolean

        Public Property Image As Bitmap
            Get
                If Not _imageDone Then
                    _imageDone = True
                    For Each it In Items
                        Try
                            _image = AlibreCommands.ImageOf(it)
                        Catch
                        End Try
                        If _image IsNot Nothing Then Exit For
                    Next
                End If
                Return _image
            End Get
            Set(value As Bitmap)
                _image = value
                _imageDone = True
            End Set
        End Property
    End Class

    Public Module AlibreCommands

        ''' <summary>Diagnostics from the last scan (written to the add-on log).</summary>
        Public LastReport As New List(Of String)

        Private _cache As List(Of FoundCommand)

        ''' <summary>Scan once and reuse it while Alibre runs (rescans until a design window has been seen).</summary>
        Public Function ScanCached() As List(Of FoundCommand)
            If _cache Is Nothing Then
                Dim sw = Diagnostics.Stopwatch.StartNew()
                Dim found = Scan()
                LastReport.Add("Scan took " & sw.ElapsedMilliseconds & " ms")
                If found.Count >= 500 Then _cache = found
                Return found
            End If
            LastReport.Clear()
            LastReport.Add("Using cached command list (" & _cache.Count & " commands)")
            Return _cache
        End Function

        Public Function Scan() As List(Of FoundCommand)
            LastReport.Clear()
            _imgDiag = 0
            _svgReady = False
            _svgFailLogged = False
            Dim result As New Dictionary(Of String, FoundCommand)
            Dim seen As New HashSet(Of Object)
            Dim forms As New List(Of Form)
            For Each f As Form In Application.OpenForms
                forms.Add(f)
            Next
            LastReport.Add("Open forms: " & forms.Count)
            For Each f In forms
                Dim ribbons As New List(Of Object)
                FindRibbons(f, ribbons, 0)
                LastReport.Add("  " & f.GetType().FullName & " '" & f.Text & "': " & ribbons.Count & " ribbon/bar managers")
                Dim fn = f.GetType().FullName
                If ribbons.Count = 0 AndAlso (fn.EndsWith("DesignEditor") OrElse fn.EndsWith("CommandCenter") OrElse fn.Contains("Editor")) Then
                    Dim budget = 120
                    DumpTree(f, 0, budget)
                    DumpFields(f)
                End If
                For Each r In ribbons
                    If seen.Contains(r) Then Continue For
                    seen.Add(r)
                    ReadItems(r, f.Text, result)
                Next
            Next
            LastReport.Add("Commands found: " & result.Count & ", with shortcut: " & result.Values.Where(Function(c) c.Shortcut <> "").Count() &
                           " (icons are rendered when shown)")
            Return result.Values.OrderBy(Function(c) c.Caption, StringComparer.CurrentCultureIgnoreCase).ToList()
        End Function

        ''' <summary>True if t is, or derives from, a DevExpress ribbon or bar manager.</summary>
        Private Function IsBarHost(t As Type) As Boolean
            While t IsNot Nothing
                Dim n = t.FullName
                If n IsNot Nothing AndAlso (n = "DevExpress.XtraBars.Ribbon.RibbonControl" OrElse n = "DevExpress.XtraBars.BarManager") Then Return True
                t = t.BaseType
            End While
            Return False
        End Function

        Private Function BaseChain(t As Type) As String
            Dim parts As New List(Of String)
            While t IsNot Nothing AndAlso parts.Count < 6
                parts.Add(t.FullName)
                t = t.BaseType
            End While
            Return String.Join(" < ", parts)
        End Function

        ' Ribbons are controls; BarManagers are components referenced from fields (of any control).
        Private Sub FindRibbons(c As Control, found As List(Of Object), depth As Integer)
            If depth > 30 Then Return
            If IsBarHost(c.GetType()) AndAlso Not found.Contains(c) Then found.Add(c)
            Dim t = c.GetType()
            While t IsNot Nothing AndAlso Not t.FullName.StartsWith("System.")
                For Each fi In t.GetFields(BindingFlags.Instance Or BindingFlags.NonPublic Or BindingFlags.Public Or BindingFlags.DeclaredOnly)
                    If fi.FieldType.IsValueType OrElse fi.FieldType Is GetType(String) Then Continue For
                    Try
                        Dim v = fi.GetValue(c)
                        If v IsNot Nothing AndAlso Not TypeOf v Is Control AndAlso IsBarHost(v.GetType()) AndAlso Not found.Contains(v) Then found.Add(v)
                    Catch
                    End Try
                Next
                t = t.BaseType
            End While
            For Each child As Control In c.Controls
                FindRibbons(child, found, depth + 1)
            Next
        End Sub

        ''' <summary>Writes the control tree of a window (types only) to the report, to see how Alibre builds it.</summary>
        Private Sub DumpTree(c As Control, depth As Integer, ByRef budget As Integer)
            If depth > 7 OrElse budget <= 0 Then Return
            budget -= 1
            LastReport.Add(New String(" "c, 4 + depth * 2) & BaseChain(c.GetType()) & If(String.IsNullOrEmpty(c.Name), "", " [" & c.Name & "]"))
            For Each child As Control In c.Controls
                DumpTree(child, depth + 1, budget)
            Next
        End Sub

        ''' <summary>Field names/types of a window that look like command, menu or ribbon holders.</summary>
        Private Sub DumpFields(o As Object)
            Dim t = o.GetType()
            While t IsNot Nothing AndAlso Not t.FullName.StartsWith("System.")
                For Each fi In t.GetFields(BindingFlags.Instance Or BindingFlags.NonPublic Or BindingFlags.Public Or BindingFlags.DeclaredOnly)
                    Dim ft = fi.FieldType.FullName
                    If ft Is Nothing Then Continue For
                    Dim l = (fi.Name & " " & ft).ToLowerInvariant()
                    If l.Contains("ribbon") OrElse l.Contains("bar") OrElse l.Contains("command") OrElse l.Contains("menu") OrElse l.Contains("shortcut") OrElse l.Contains("toolbar") OrElse l.Contains("action") Then
                        Dim v As Object = Nothing
                        Try
                            v = fi.GetValue(o)
                        Catch
                        End Try
                        LastReport.Add("    field " & t.Name & "." & fi.Name & " : " & ft & If(v Is Nothing, " = null", " = " & v.GetType().FullName))
                    End If
                Next
                t = t.BaseType
            End While
        End Sub

        Private Sub ReadItems(owner As Object, windowText As String, result As Dictionary(Of String, FoundCommand))
            Dim items As IEnumerable = Nothing
            Try
                items = owner.Items
            Catch ex As Exception
                LastReport.Add("    no Items on " & owner.GetType().FullName & ": " & ex.Message)
                Return
            End Try
            Dim pageOf = PageMap(owner)
            Dim n = 0
            For Each it In items
                n += 1
                Try
                    Dim tn = it.GetType().FullName
                    If Not (tn.Contains("BarButtonItem") OrElse tn.Contains("BarLargeButtonItem") OrElse tn.Contains("BarCheckItem")) Then Continue For
                    Dim caption = CStr(it.Caption)
                    Dim title = TipTitle(it)
                    If title <> "" Then caption = title
                    If String.IsNullOrWhiteSpace(caption) Then Continue For
                    caption = caption.Replace("&", "").Trim()
                    Dim name As String = ""
                    Try
                        If TypeOf it.Tag Is String Then name = CStr(it.Tag)
                    Catch
                    End Try
                    If String.IsNullOrEmpty(name) Then name = CStr(it.Name)
                    If String.IsNullOrEmpty(name) Then name = caption
                    Dim key = name & "|" & caption
                    Dim fc As FoundCommand = Nothing
                    If Not result.TryGetValue(key, fc) Then
                        fc = New FoundCommand With {.Id = name, .Caption = caption, .Window = windowText}
                        result(key) = fc
                    End If
                    If fc.Shortcut = "" Then fc.Shortcut = ShortcutOf(it)
                    If fc.Shortcut = "" Then fc.Shortcut = ShortcutFromHint(it)
                    If fc.Page = "" Then
                        Dim p As String = Nothing
                        If pageOf.TryGetValue(it, p) Then fc.Page = p
                    End If
                    If fc.Items.Count < 3 Then fc.Items.Add(it)
                Catch
                End Try
            Next
            LastReport.Add("    " & owner.GetType().Name & ": " & n & " items")
        End Sub

        ' item -> "Page / Group" for ribbons
        Private Function PageMap(owner As Object) As Dictionary(Of Object, String)
            Dim map As New Dictionary(Of Object, String)
            Try
                For Each page In owner.Pages
                    For Each grp In page.Groups
                        For Each link In grp.ItemLinks
                            Try
                                Dim item = link.Item
                                If item IsNot Nothing AndAlso Not map.ContainsKey(item) Then map(item) = CStr(page.Text) & " / " & CStr(grp.Text)
                            Catch
                            End Try
                        Next
                    Next
                Next
            Catch
            End Try
            Return map
        End Function

        ' First line of the screen tip, e.g. "Extrude Boss"
        Private Function TipTitle(it As Object) As String
            Try
                Dim tip = it.SuperTip
                If tip Is Nothing Then Return ""
                For Each t In tip.Items
                    Dim txt = CStr(t.Text)
                    If Not String.IsNullOrWhiteSpace(txt) Then
                        txt = txt.Trim()
                        If txt.Length <= 40 AndAlso Not txt.Contains(".") Then Return txt
                        Return ""
                    End If
                Next
            Catch
            End Try
            Return ""
        End Function

        Private Function ShortcutOf(it As Object) As String
            Try
                Dim sc = it.ItemShortcut
                If sc Is Nothing Then Return ""
                Dim k As Keys = CType(sc.Key, Keys)
                If k = Keys.None Then Return ""
                Return KeyStroke.FromKeys(k)
            Catch
                Return ""
            End Try
        End Function

        ' Alibre writes "(Ctrl+Alt+B)" at the end of the screen tip text
        Private Function ShortcutFromHint(it As Object) As String
            Dim texts As New List(Of String)
            Try
                texts.Add(CStr(it.Hint))
            Catch
            End Try
            Try
                Dim tip = it.SuperTip
                If tip IsNot Nothing Then
                    For Each t In tip.Items
                        Try
                            texts.Add(CStr(t.Text))
                        Catch
                        End Try
                    Next
                End If
            Catch
            End Try
            For Each t In texts
                If String.IsNullOrEmpty(t) Then Continue For
                Dim m = System.Text.RegularExpressions.Regex.Match(t, "\(((?:Ctrl|Alt|Shift)(?:\+(?:Ctrl|Alt|Shift))*\+[^\)\s]+|F\d{1,2})\)\s*$")
                If m.Success Then
                    Dim s = m.Groups(1).Value.ToUpperInvariant().Replace("DEL", "DELETE").Replace("DELETEETE", "DELETE")
                    Dim mods As List(Of String) = Nothing
                    Dim usage As Integer
                    If KeyStroke.Parse(s, mods, usage) Then Return s
                End If
            Next
            Return ""
        End Function

        Friend Function ImageOf(it As Object) As Bitmap
            ' 1) raster images
            For Each getter In New Func(Of Object)() {
                    Function() it.ImageOptions.LargeImage,
                    Function() it.ImageOptions.Image,
                    Function() it.LargeGlyph,
                    Function() it.Glyph}
                Try
                    Dim img = TryCast(getter(), Image)
                    If img IsNot Nothing Then Return New Bitmap(img)
                Catch
                End Try
            Next
            ' 2) image lists
            Try
                Dim idx As Integer = it.ImageOptions.LargeImageIndex
                If idx >= 0 Then
                    Dim img = ImageFromCollection(it.Manager.LargeImages, idx)
                    If img IsNot Nothing Then Return img
                End If
            Catch
            End Try
            Try
                Dim idx As Integer = it.ImageOptions.ImageIndex
                If idx >= 0 Then
                    Dim img = ImageFromCollection(it.Manager.Images, idx)
                    If img IsNot Nothing Then Return img
                End If
            Catch
            End Try
            ' 3) SVG images
            Try
                Dim svg = it.ImageOptions.SvgImage
                If svg IsNot Nothing Then Return RenderSvg(svg)
            Catch
            End Try
            Return Nothing
        End Function

        Private _imgDiag As Integer
        Private Sub DescribeImage(it As Object, caption As String)
            LastReport.Add("    image diag '" & caption & "' " & it.GetType().FullName)
            For Each pth In {"ImageOptions.Image", "ImageOptions.LargeImage", "ImageOptions.ImageIndex", "ImageOptions.LargeImageIndex",
                             "ImageOptions.SvgImage", "ImageOptions.ImageUri", "ImageOptions.Images", "ImageOptions.LargeImages",
                             "Glyph", "LargeGlyph", "ImageIndex", "LargeImageIndex", "Manager.Images", "Manager.LargeImages", "Tag"}
                Dim v As Object = it
                Try
                    For Each part In pth.Split("."c)
                        v = v.GetType().InvokeMember(part, BindingFlags.GetProperty Or BindingFlags.Public Or BindingFlags.Instance, Nothing, v, Nothing)
                        If v Is Nothing Then Exit For
                    Next
                    LastReport.Add("      " & pth & " = " & If(v Is Nothing, "null", v.GetType().FullName & " : " & v.ToString()))
                Catch ex As Exception
                    LastReport.Add("      " & pth & " ! " & ex.GetType().Name)
                End Try
            Next
            ' Alibre-specific properties on the item type
            For Each pi In it.GetType().GetProperties(BindingFlags.Instance Or BindingFlags.Public Or BindingFlags.NonPublic Or BindingFlags.DeclaredOnly)
                Try
                    Dim v = pi.GetValue(it, Nothing)
                    LastReport.Add("      own " & pi.Name & " : " & pi.PropertyType.Name & " = " & If(v Is Nothing, "null", v.ToString()))
                Catch
                End Try
            Next
            For Each fi In it.GetType().GetFields(BindingFlags.Instance Or BindingFlags.Public Or BindingFlags.NonPublic Or BindingFlags.DeclaredOnly)
                Try
                    Dim v = fi.GetValue(it)
                    LastReport.Add("      field " & fi.Name & " : " & fi.FieldType.Name & " = " & If(v Is Nothing, "null", v.ToString()))
                Catch
                End Try
            Next
        End Sub

        Private Function ImageFromCollection(images As Object, idx As Integer) As Bitmap
            If images Is Nothing Then Return Nothing
            Try
                Dim il = TryCast(images, ImageList)
                If il IsNot Nothing Then Return New Bitmap(il.Images(idx))
                Dim img = TryCast(images.Images(idx), Image)
                If img IsNot Nothing Then Return New Bitmap(img)
            Catch
            End Try
            Return Nothing
        End Function

        Private _svgReady As Boolean
        Private _svgBitmapType As Type
        Private _svgFailLogged As Boolean

        Private Function RenderSvg(svg As Object) As Bitmap
            If Not _svgReady Then
                _svgReady = True
                For Each a In AppDomain.CurrentDomain.GetAssemblies()
                    If Not a.FullName.StartsWith("DevExpress") Then Continue For
                    Try
                        For Each t In a.GetTypes()
                            If t.Name = "SvgBitmap" AndAlso t.IsClass Then
                                _svgBitmapType = t
                                Exit For
                            End If
                        Next
                    Catch
                    End Try
                    If _svgBitmapType IsNot Nothing Then Exit For
                Next
                LastReport.Add("    SvgBitmap type: " & If(_svgBitmapType Is Nothing, "not found", _svgBitmapType.AssemblyQualifiedName))
                If _svgBitmapType IsNot Nothing Then
                    For Each m In _svgBitmapType.GetMethods(BindingFlags.Public Or BindingFlags.Instance Or BindingFlags.Static)
                        If m.Name = "Create" OrElse m.Name = "Render" OrElse m.Name.StartsWith("Render") Then
                            LastReport.Add("      " & If(m.IsStatic, "static ", "") & m.Name & "(" &
                                String.Join(", ", m.GetParameters().Select(Function(q) q.ParameterType.Name & " " & q.Name)) & ") As " & m.ReturnType.Name)
                        End If
                    Next
                End If
            End If
            If _svgBitmapType Is Nothing Then Return Nothing
            Try
                ' get an SvgBitmap: constructor(SvgImage) or static Create(SvgImage)
                Dim sb As Object = Nothing
                Dim ctor = _svgBitmapType.GetConstructor(New Type() {svg.GetType()})
                If ctor IsNot Nothing Then
                    sb = ctor.Invoke(New Object() {svg})
                Else
                    For Each m In _svgBitmapType.GetMethods(BindingFlags.Public Or BindingFlags.Static)
                        If m.Name = "Create" AndAlso m.GetParameters().Length = 1 AndAlso m.GetParameters()(0).ParameterType.IsAssignableFrom(svg.GetType()) Then
                            sb = m.Invoke(Nothing, New Object() {svg})
                            Exit For
                        End If
                    Next
                End If
                If sb Is Nothing Then Return Nothing
                ' Render(...): fill parameters - palette = Nothing, scale = 2, Size = 48x48, other = default
                For Each m In _svgBitmapType.GetMethods(BindingFlags.Public Or BindingFlags.Instance).Where(Function(x) x.Name = "Render").OrderBy(Function(x) x.GetParameters().Length)
                    Dim ps = m.GetParameters()
                    Dim args(ps.Length - 1) As Object
                    Dim ok = True
                    For k = 0 To ps.Length - 1
                        Dim pt = ps(k).ParameterType
                        If pt Is GetType(Double) Then
                            args(k) = 2.0R
                        ElseIf pt Is GetType(Single) Then
                            args(k) = 2.0F
                        ElseIf pt Is GetType(Size) Then
                            args(k) = New Size(48, 48)
                        ElseIf Not pt.IsValueType Then
                            args(k) = Nothing
                        ElseIf ps(k).HasDefaultValue Then
                            args(k) = ps(k).DefaultValue
                        Else
                            Try
                                args(k) = Activator.CreateInstance(pt)
                            Catch
                                ok = False
                            End Try
                        End If
                    Next
                    If Not ok Then Continue For
                    Try
                        Dim img = TryCast(m.Invoke(sb, args), Image)
                        If img IsNot Nothing Then Return New Bitmap(img)
                    Catch ex As Exception
                        If Not _svgFailLogged Then
                            _svgFailLogged = True
                            LastReport.Add("    Render failed: " & If(ex.InnerException, ex).Message)
                        End If
                    End Try
                Next
            Catch ex As Exception
                If Not _svgFailLogged Then
                    _svgFailLogged = True
                    LastReport.Add("    SVG render failed: " & ex.Message)
                End If
            End Try
            Return Nothing
        End Function

        ' ---------------------------------------------------------------- run a command

        ' per window: command name -> ribbon item, and caption -> ribbon item
        Private ReadOnly _itemsByForm As New Dictionary(Of Form, Tuple(Of Dictionary(Of String, Object), Dictionary(Of String, Object)))

        ''' <summary>The ribbon item of this window for a command (by its name, else by its caption).</summary>
        Public Function FindItem(form As Form, command As String, caption As String) As Object
            For Each dead In _itemsByForm.Keys.Where(Function(f) f.IsDisposed).ToList()
                _itemsByForm.Remove(dead)
            Next
            Dim maps As Tuple(Of Dictionary(Of String, Object), Dictionary(Of String, Object)) = Nothing
            If Not _itemsByForm.TryGetValue(form, maps) Then
                maps = Tuple.Create(New Dictionary(Of String, Object)(StringComparer.OrdinalIgnoreCase),
                                    New Dictionary(Of String, Object)(StringComparer.OrdinalIgnoreCase))
                Dim ribbons As New List(Of Object)
                FindRibbons(form, ribbons, 0)
                For Each r In ribbons
                    Try
                        For Each it In r.Items
                            Try
                                Dim tn = it.GetType().FullName
                                If Not (tn.Contains("BarButtonItem") OrElse tn.Contains("BarLargeButtonItem") OrElse tn.Contains("BarCheckItem")) Then Continue For
                                Dim name As String = ""
                                Try
                                    If TypeOf it.Tag Is String Then name = CStr(it.Tag)
                                Catch
                                End Try
                                If String.IsNullOrEmpty(name) Then name = CStr(it.Name)
                                If Not String.IsNullOrEmpty(name) AndAlso Not maps.Item1.ContainsKey(name) Then maps.Item1(name) = it
                                Dim cap = TipTitle(it)
                                If cap = "" Then cap = CStr(it.Caption)
                                If Not String.IsNullOrEmpty(cap) Then
                                    cap = cap.Replace("&", "").Trim()
                                    If Not maps.Item2.ContainsKey(cap) Then maps.Item2(cap) = it
                                End If
                            Catch
                            End Try
                        Next
                    Catch
                    End Try
                Next
                If maps.Item1.Count + maps.Item2.Count > 0 Then _itemsByForm(form) = maps
            End If
            Dim found As Object = Nothing
            If Not String.IsNullOrEmpty(command) AndAlso maps.Item1.TryGetValue(command, found) Then Return found
            If Not String.IsNullOrEmpty(caption) AndAlso maps.Item2.TryGetValue(caption.Trim(), found) Then Return found
            Return Nothing
        End Function

        ''' <summary>Clicks a ribbon item, like the user would. False if it is disabled or cannot be clicked.</summary>
        Public Function Click(item As Object) As Boolean
            Try
                If Not CBool(item.Enabled) Then Return False
            Catch
            End Try
            item.PerformClick()
            Return True
        End Function

        ''' <summary>Guess Part/Assembly/Sketch/Drawing/General from the ribbon page and window.</summary>
        Public Function GuessCategory(fc As FoundCommand) As String
            Dim s = (fc.Page & " " & fc.Window).ToLowerInvariant()
            If s.Contains("sketch") Then Return "Sketch"
            If s.Contains("drawing") OrElse s.Contains("sheet") OrElse s.Contains(".ad_drw") Then Return "Drawing"
            If s.Contains("assembl") OrElse s.Contains(".ad_asm") Then Return "Assembly"
            If s.Contains("feature") OrElse s.Contains(".ad_prt") Then Return "Part"
            Return "General"
        End Function
    End Module

End Namespace
