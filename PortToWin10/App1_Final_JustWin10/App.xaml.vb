﻿Imports Windows.ApplicationModel.Store
Imports Windows.Storage

NotInheritable Class App
    Inherits Application

    ' TRANSIENT STATE
    Shared RND As New Random()
    Shared _HasSpeedBoost As Boolean? = Nothing
    Shared BeepFile As StorageFile
    Shared wavePlayer As New wavePlayer

    ' LOCAL STATE
    Public Shared ix, iy, rz As Integer
    Public Shared dx, dy, dr As Integer

    ' ROAMING STATE
    Public Shared useBeachBall As Boolean = False


    Protected Overrides Sub OnLaunched(e As Windows.ApplicationModel.Activation.LaunchActivatedEventArgs)
        AddHandler ApplicationData.Current.DataChanged, AddressOf LoadRoamingState
        StartInitializationAsync()
        '
        Dim rootFrame As Frame = TryCast(Window.Current.Content, Frame)
        If rootFrame Is Nothing Then
            rootFrame = New Frame()
            LoadState()
            'If e.PreviousExecutionState = ApplicationExecutionState.Terminated Then ...
            Window.Current.Content = rootFrame
        End If
        If rootFrame.Content Is Nothing Then
            rootFrame.Navigate(GetType(MainPage), e.Arguments)
        End If
        Window.Current.Activate()
    End Sub

    Private Sub OnSuspending(sender As Object, e As SuspendingEventArgs) Handles Me.Suspending
        Dim deferral As SuspendingDeferral = e.SuspendingOperation.GetDeferral()
        RemoveHandler ApplicationData.Current.DataChanged, AddressOf LoadRoamingState
        SaveState()
        wavePlayer.Dispose() : wavePlayer = Nothing
        deferral.Complete()
    End Sub


    Shared Sub LoadState()
        Dim v = ApplicationData.Current.LocalSettings.Values("dx")
        dx = If(v Is Nothing, RND.Next(20), CInt(v))
        v = ApplicationData.Current.LocalSettings.Values("dy")
        dy = If(v Is Nothing, RND.Next(20), CInt(v))
        v = ApplicationData.Current.LocalSettings.Values("dr")
        dr = If(v Is Nothing, RND.Next(30), CInt(v))
        ix = CInt(ApplicationData.Current.LocalSettings.Values("ix"))
        iy = CInt(ApplicationData.Current.LocalSettings.Values("iy"))
        rz = CInt(ApplicationData.Current.LocalSettings.Values("rz"))
        '
        LoadRoamingState(ApplicationData.Current, Nothing)
    End Sub

    Shared Sub LoadRoamingState(d As ApplicationData, o As Object)
        useBeachBall = CBool(d.RoamingSettings.Values("useBeachBall"))
    End Sub

    Shared Sub SaveState()
        ApplicationData.Current.LocalSettings.Values("dx") = dx
        ApplicationData.Current.LocalSettings.Values("dy") = dy
        ApplicationData.Current.LocalSettings.Values("dr") = dr
        ApplicationData.Current.LocalSettings.Values("ix") = ix
        ApplicationData.Current.LocalSettings.Values("iy") = iy
        ApplicationData.Current.LocalSettings.Values("rz") = rz
        '
        ApplicationData.Current.RoamingSettings.Values("useBeachBall") = useBeachBall
    End Sub


    Shared ReadOnly Property HasSpeedBoost As Boolean
        Get
            Try
                If _HasSpeedBoost Is Nothing Then _HasSpeedBoost = CurrentApp.LicenseInformation.ProductLicenses("SpeedBoost").IsActive
            Catch ex As Exception
                _HasSpeedBoost = False
            End Try
            Return _HasSpeedBoost.Value
        End Get
    End Property

    Shared Async Function PurchaseSpeedBoostAsync() As Task
        If HasSpeedBoost() Then Return
        _HasSpeedBoost = Nothing
        Dim log = CStr(ApplicationData.Current.LocalSettings.Values("log"))
        If log IsNot Nothing Then
            ' previous run of this app tried to purchase, but didn't succeed...
            ApplicationData.Current.LocalSettings.Values.Remove("log")
            SendErrorReport(log)
            _HasSpeedBoost = True ' so the user can at least use the item
            Return
        End If

        Try
            log = "About to await RequestProductPurchaseAsync"
            ApplicationData.Current.LocalSettings.Values("log") = log
            Dim result = Await CurrentApp.RequestProductPurchaseAsync("SpeedBoost")
            log &= vbCrLf & String.Format("Finished await. Status={0}, OfferId={1}, TransactionId={2}",
                                          result.Status, result.OfferId, result.TransactionId)
            ApplicationData.Current.LocalSettings.Values("log") = log
        Catch ex As Exception
            log &= vbCrLf & "EXCEPTION! " & ex.Message & ex.StackTrace
            ApplicationData.Current.LocalSettings.Values("log") = log
            SendErrorReport(ex)
        End Try
    End Function

    Shared Sub SendErrorReport(ex As Exception)
        SendErrorReport(ex.Message & vbCrLf & "stack:" & vbCrLf & ex.StackTrace)
    End Sub

    Shared Async Sub SendErrorReport(msg As String)
        Dim md As New Windows.UI.Popups.MessageDialog("Oops. There's been an internal error", "Bug report")
        Dim r As Boolean? = Nothing
        md.Commands.Add(New Windows.UI.Popups.UICommand("Send bug report", Sub() r = True))
        md.Commands.Add(New Windows.UI.Popups.UICommand("Cancel", Sub() r = False))
        Await md.ShowAsync()
        If Not r.HasValue OrElse Not r.Value Then Return
        '
        Dim emailTo = "lu@wischik.com"
        Dim emailSubject = "App1 problem report"
        Dim emailBody = "I encountered a problem with App1..." & vbCrLf & vbCrLf & msg
        Dim url = "mailto:?to=" & emailTo & "&subject=" & emailSubject & "&body=" & Uri.EscapeDataString(emailBody)
        Await Windows.System.Launcher.LaunchUriAsync(New Uri(url))
    End Sub


    Shared Sub Tick(maxWidth As Double, maxHeight As Double)
        ix += dx : iy += dy : rz += dr
        If HasSpeedBoost() Then ix += dx : iy += dy : rz += dr
        Dim hasBounced = False
        If ix < 0 Then dx = Math.Abs(dx) : hasBounced = True
        If ix > maxWidth Then dx = -Math.Abs(dx) : hasBounced = True
        If iy < 0 Then dy = Math.Abs(dy) : hasBounced = True
        If iy > maxHeight Then dy = -Math.Abs(dy) : hasBounced = True
        If hasBounced Then
            dx += RND.Next(10) - 5 : dy += RND.Next(10) - 5 : dr = -dr + RND.Next(10) - 5
            If BeepFile IsNot Nothing Then wavePlayer.StartPlay(BeepFile)
        End If
    End Sub


    Shared Async Sub StartInitializationAsync()
        Try
            Await Task.Run(Async Function()
                               Dim folder = Await Package.Current.InstalledLocation.GetFolderAsync("Assets")
                               BeepFile = Await folder.GetFileAsync("beep.wav")
                           End Function)
        Catch ex As Exception
            SendErrorReport(ex)
        End Try
    End Sub

    Sub OnResuming(sender As Object, e As Object) Handles Me.Resuming
        If wavePlayer Is Nothing Then wavePlayer = New wavePlayer()
    End Sub


End Class

