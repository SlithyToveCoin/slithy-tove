function RuntimeFileAttributes(FileName: String): Cardinal;
  external 'GetFileAttributesW@kernel32.dll stdcall';

function RuntimePathIsPlain(FileName: String): Boolean;
var
  Parent: String;
  Attributes: Cardinal;
begin
  Result := False;
  while FileName <> '' do begin
    Attributes := RuntimeFileAttributes(FileName);
    if (Attributes = $FFFFFFFF) or ((Attributes and $400) <> 0) then
      Exit;
    Parent := ExtractFileDir(FileName);
    if Parent = FileName then
      Break;
    FileName := Parent;
  end;
  Result := True;
end;

procedure RemovePriorRuntimeFile(RelativePath, ExpectedHash: String);
var
  Root, FileName: String;
begin
  { A known filename and hash prevent removing a user's replacement file. }
  if (RelativePath = '') or (Pos('..', RelativePath) > 0) or
     (Pos(':', RelativePath) > 0) or (Pos('/', RelativePath) > 0) or
     (Pos('*', RelativePath) > 0) or (Pos('?', RelativePath) > 0) or
     (RelativePath[1] = '\') or (Length(ExpectedHash) <> 64) then begin
    Log('Skipped an invalid obsolete-runtime entry.');
    Exit;
  end;
  Root := AddBackslash(ExpandFileName(ExpandConstant('{app}')));
  FileName := ExpandFileName(Root + RelativePath);
  if CompareText(Copy(FileName, 1, Length(Root)), Root) <> 0 then
    Exit;
  if not RuntimePathIsPlain(ExtractFileDir(FileName)) then begin
    Log('Preserved obsolete-runtime path with a link or unreadable parent: ' + RelativePath);
    Exit;
  end;
  if not FileExists(FileName) then
    Exit;
  try
    if not RuntimePathIsPlain(FileName) then begin
      Log('Preserved obsolete-runtime path with a link or unreadable parent: ' + RelativePath);
      Exit;
    end;
    if CompareText(GetSHA256OfFile(FileName), ExpectedHash) <> 0 then begin
      Log('Preserved modified obsolete-runtime file: ' + RelativePath);
      Exit;
    end;
    if DeleteFile(FileName) then
      Log('Removed obsolete-runtime file: ' + RelativePath)
    else
      Log('Could not remove obsolete-runtime file: ' + RelativePath);
  except
    Log('Preserved unreadable obsolete-runtime file: ' + RelativePath);
  end;
end;
