Remove-Item PublishOutput -Recurse -ErrorAction Ignore
dotnet publish ../ProtoHackers.csproj /p:IsTransformWebConfigDisabled=true -c Release -o .\PublishOutput -r linux-x64 --self-contained false
tar -czvf Publish.tar.gz PublishOutput
scp Publish.tar.gz deployserver.ps1 protohackers.service brnls@45.33.108.77:/home/brnls/
ssh brnls@45.33.108.77 'pwsh deployserver.ps1'
Remove-Item Publish.tar.gz