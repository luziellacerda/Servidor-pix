# Verificação da candidata Station multiplayer

Executar a partir da raiz do repositório:

```sh
dotnet run --project tests/StationMultiplayer/StationMultiplayer.Tests.csproj -c Release
dotnet run --project tests/StationMultiplayer/StationMultiplayer.Http.Tests.csproj -c Release
dotnet run --project tests/StationRecovery/StationRecovery.Tests.csproj -c Release
```

O primeiro programa verifica catálogo/assinatura/vínculo de conteúdo, estado, codec, budget e aposentadoria das janelas. O segundo usa HTTP/TLS/WSS em loopback com quatro usuários e seis conexões sintéticos, provas vinculadas, desconexão/retomada e chat. O terceiro mantém os vetores v2 e testes de observabilidade existentes.

Não usa licenças de clientes, jogos, dispositivos Android ou banco de produção. Não constitui teste de gameplay ou de Internet. As flags v3 e o cadastro de perfis reais não são ativados pelos testes. A divergência histórica TLS v2 sem logging exige investigação separada antes de implantação.
