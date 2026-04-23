Sub ExtractEmailAddressesFromFolder()
    Dim oFolder As Outlook.MAPIFolder
    Dim oItems As Outlook.Items
    Dim oMail As Outlook.MailItem
    Dim oReport As Outlook.ReportItem
    Dim oItem As Object

    Dim cutoffDate As Date
    Dim uniqueEmails As New Collection
    Dim outputPath As String
    Dim fileNum As Integer
    Dim i As Long
    Dim totalItems As Long

    Dim oRegex As Object
    Dim oMatches As Object
    Dim oMatch As Object
    Dim bodyText As String

    cutoffDate = #1/1/2026#
    outputPath = Environ("USERPROFILE") & "\Desktop\extracted_emails.txt"

    Set oRegex = CreateObject("VBScript.RegExp")
    oRegex.Global = True
    oRegex.IgnoreCase = True
    oRegex.Pattern = "[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}"

    ' Get currently selected folder
    Set oFolder = Application.ActiveExplorer.CurrentFolder
    If oFolder Is Nothing Then
        MsgBox "No folder selected.", vbExclamation
        Exit Sub
    End If

    Set oItems = oFolder.Items
    totalItems = oItems.Count

    If totalItems = 0 Then
        MsgBox "Folder is empty.", vbInformation
        Exit Sub
    End If

    For i = 1 To totalItems
        Set oItem = oItems.Item(i)

        If oItem.Class = olMail Then
            Set oMail = oItem
            If oMail.ReceivedTime < cutoffDate Then
                bodyText = oMail.Body
                If Len(Trim(bodyText)) = 0 Then
                    bodyText = StripHtml(oMail.HTMLBody)
                End If
                Set oMatches = oRegex.Execute(bodyText)
                For Each oMatch In oMatches
                    AddUnique uniqueEmails, LCase(Trim(oMatch.Value))
                Next oMatch
            End If
        ElseIf oItem.Class = 46 Then  ' olReport
            Set oReport = oItem
            If oReport.ReceivedTime < cutoffDate Then
                bodyText = oReport.Body
                If Len(Trim(bodyText)) = 0 Then
                    bodyText = StripHtml(oReport.HTMLBody)
                End If
                Set oMatches = oRegex.Execute(bodyText)
                For Each oMatch In oMatches
                    AddUnique uniqueEmails, LCase(Trim(oMatch.Value))
                Next oMatch
            End If
        End If
    Next i

    If uniqueEmails.Count = 0 Then
        MsgBox "No email addresses found in message bodies older than 1 Jan 2026.", vbInformation
        Exit Sub
    End If

    fileNum = FreeFile
    Open outputPath For Output As #fileNum
    Dim addr As Variant
    For Each addr In uniqueEmails
        Print #fileNum, addr
    Next addr
    Close #fileNum

    MsgBox "Done! " & uniqueEmails.Count & " unique addresses written to:" & vbCrLf & outputPath, vbInformation
End Sub

Private Function StripHtml(html As String) As String
    Dim oRegex As Object
    Set oRegex = CreateObject("VBScript.RegExp")
    oRegex.Global = True
    oRegex.Pattern = "<[^>]+>"
    StripHtml = oRegex.Replace(html, " ")
End Function

Private Sub AddUnique(col As Collection, val As String)
    Dim v As Variant
    For Each v In col
        If v = val Then Exit Sub
    Next v
    col.Add val
End Sub
