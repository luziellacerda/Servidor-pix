# Testes isolados do relay R12

Não iniciam produção nem usam banco real. Na raiz do repo, .NET8:

```
dotnet run --project docs/station-android/relay-r12-tests/Regression.csproj -c Release
dotnet run --project docs/station-android/relay-r12-tests/Http.csproj -c Release
```

Integration.csproj inicia HTTPS em loopback com certificado efêmero e chama o bridge Java real. Recebe três argumentos após `--`: diretório temporário absoluto, executável Java17, classpath contendo RelayBridgeTests compilado + netplay-internet.jar do app + station-client.jar do R10. Para preparar esses arquivos, seguir run_station_relay_tests.py no commit do app vinculado no handoff. Não usar certificados/identidades de produção nem desativar TLS do aplicativo. No Windows, o teste importa o certificado PFX efêmero no provedor compatível com Schannel; nada disso vai no servidor de produção/APK.

39 regressões +37 HTTP+17 integração+7 bridge passaram no workspace Windows. Somente homologação com dois aparelhos e redes externas pode provar a partida real. Evidência em evidence/ do app.
