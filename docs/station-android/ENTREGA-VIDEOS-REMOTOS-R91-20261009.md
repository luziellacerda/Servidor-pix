# Entrega R91 — vídeos de carrossel atualizáveis

APP → SERVIDOR. Código pronto e testado localmente; **não implantado**. Entrega aditiva, sem novo cadastro de motor. Base exata do servidor: `2b04f591eb10ad76efc3b630ded4fff9ef2c2a27`. Não restaurar os 209 Java da entrega cinco jogadores sobre a R91 de 220 fontes.

App fonte: [TurboElden 4276a5fa](https://github.com/luziellacerda/TurboElden/commit/4276a5fa0af77a106bca2cab67116a34efe83501), branch `fix/station-remote-media-r91-20261009`. Samsung R91 instalado às 13:40:48 UTC de 09/10/2026, APK SHA `af02c36dc143f1e82a683f86f4fa45240952eb621ed8da388298b82bf9436d1c`. Motorola permanece R86. Licença, dados, UID e motores preservados.

## Entrega executável

1. `src/TurboRamaSuiteOnlineServer/StationCarouselMedia.cs`: índice validado, recarga de revisão, duas rotas autenticadas.
2. `StationEndpoints.cs`: única adição `app.MapStationCarouselMedia(enabled);` à entrada MapStation.
3. `carousel-media-r91-20261009/publish_media.py`: valida e publica arquivos de forma atômica, preservando os demais por padrão.
4. Manifesto e recibos em `carousel-media-r91-20261009/`; nenhum vídeo, APK, DLL, ROM, BIOS, licença ou log pessoal no Git.

## Contrato exato

- GET `/v1/station/media/catalog?requestId=<32 hex minúsculos>`; autenticação de sessão e prova já existentes. Envelope assinado por StationResponseSigner, `domain=TurboRamaStationAndroid/media-catalog/v1`, `schemaVersion=1`, produto/aplicação/licença/aparelho/sessão, eco do requestId, revision crescente, items completo.
- GET `/v1/station/media/files/<64 hex minúsculos>`; sessão e prova normais, mesma origem. Content-Type video/mp4, Content-Length exato. O SHA-256 vem do catálogo assinado; o Android confere bytes e formato antes de publicar o cache.
- Item: asset, sha256, sizeBytes, contentType, width=720, height=720, fps=30, audioTracks=0. Asset só admite `turbo-system-videos/720-[a-z0-9][a-z0-9_-]{0,95}.mp4`; nunca URL ou caminho livre.
- Até 128 itens,32MiB por arquivo,256MiB por catálogo; nonce novo por requisição. 503 enquanto desativado/sem índice válido;404 para hash ausente;429 por limite de origem. Cache-Control no-store para respostasHTTP; cache persistente pertence aoapp.
- Índice do disco usa schemaVersion/revision/items, consultado no máximo uma vez por60s sob gate assíncrono. Todos os arquivos/tamanhos/assinatura MP4/hashes são validados antes de trocar o snapshot. Índice inválido ou revisão não crescente conserva o último válido. Links simbólicos/reparse são recusados.

## Ativação pelo operador

Esta entrega não alterou Linux, serviços, banco, proxy, firewall ou outros produtos. O operador deve conciliar o commit com a fonte realmente ativa e coordenar partidas antes da implantação inicial, pois o serviço mantém sessões em memória.

1. Integrar estes dois arquivos ao build atual da API Station e verificar a compilação net8 normal. O módulo não pede migration, nova identidade de motor nem mudança em rotas existentes.
2. Copiar por canal privado os 58 MP4 imutáveis e index.json do pacote local abaixo para um diretório exclusivo de mídia, sem symlinks. Usuário do serviço precisa apenas ler. Conservar a cópia anterior para retorno.
3. Configurar `Station:CarouselMediaDirectory` com esse caminho local e `Station:CarouselMediaEnabled=true`. Padrão false. Habilitar junto da implantação inicial coordenada. O tráfego usa o mesmo host/TLS/prova `/v1/station`; confirmar que a regra já aplicada ao prefixo permite essas duas rotas, sem abrir acesso público a arquivos.
4. Confirmar pelo aplicativo autenticado um catálogo fresco assinado, nonce correspondente, revision202610092 e58itens; conferir SHA/tamanho de ao menos uma resposta binária. Em seguida deixar carregar no menu, sair/voltar e verificar reprodução offline. Não usar HTTP200 isolado ou resposta emcache como prova.
5. Registrar commit/DLL/PID/UTC ativos e catálogo realmente observado. Somente depois podemos remover MP4 embutidos de uma futura versão do APK. Esta R91 conserva os vídeos locais de transição.

## Arquivos privados prontos

Pacote local: `G:\BAKUP SISTEMA APP 03-10-2026\ATUAL-2P-E-TESTE-4P-20261008\maintenance\r91-server-media\station-carousel-media-202610092.zip`

ZIP SHA-256: `74c99f7013a972c06bd3ef3dd53d5bf6542b038b06d8045356a73ac6ef9b0052`. Contém58vídeos+index.json, sem outros ativos. ÍndiceSHA `827dfe47fbd446bccd1fe715f97dd1b0d89e7ce6317a809f0f318ef198b77e83`; revision202610092;109.503.389 bytes de vídeo. O APK mantém outro MP4 de abertura, fora deste catálogo. Transferência ao servidor ainda não efetuada; pacote não está noGit.

## Atualizações futuras e retorno

Com Python3.11+ e ffprobe, executar localmente ao diretório de publicação:

```text
python publish_media.py --source NOVOS_VIDEOS --output DIRETORIO_MIDIA --revision 202610093
```

NOVOS_VIDEOS contém `720-*.mp4` H.264720x720,30fps,semáudio. O publicador mescla a nova seleção, mantém os outros itens e publica índice por rename atômico somente após validar. `--replace-all` substitui intencionalmente todo o índice. Arquivos antigos são mantidos para leitores e retorno; não apagar imediatamente. Mudança no índice é recarregada sem reinício após a ativação inicial; o app consulta em até5min enquanto no menu e usa o vídeo novo na seleção seguinte. Novos nomes de plataformas ainda precisam existir no mapeamento doapp.

Para desfazer uma atualização de conteúdo, republicar os hashes anteriores com revisão MAIOR; o índice não aceita retrocesso. Para desabilitar toda a funcionalidade, desligar a flag e recarregar configuração via procedimento coordenado da API; o app conserva cache/bundledfallback. Nunca limpar licenças ou mudar assinatura. O cache antigo fica acessível offline; esta entrega não fornece revogação remota de um vídeo já validado.

## Evidência e limites

Servidor net8 compilou sem warnings/erros; DLL localSHA `c1de870aeaa12b33598cf5bd34ebefbad14309192899dbd98ba9d6392fc3a247`.20checks do registro (19sintéticos+58MP4reais em uma verificação); publicador preservou57itens ao atualizar1. Fixture executada comSDK9; produção continua net8. Para reproduzir: `dotnet run --project docs/station-android/carousel-media-r91-20261009/StationMedia.Tests.csproj -- DIRETORIO_TEMPORARIO [DIRETORIO_MIDIA]`.

Android:645checksdownloads,45mídia,5guardas; catálogo falso/nonce/sessão/hash/tamanho/codec/cancelamento recusados; atualização incompleta preserva a anterior. Recompilação do backup Java/carrossel idêntica aoAPK instalado. Os testes não afirmam produção ativada, corte físico de rede, redução térmica nem qualificação adicional doonline.

Capas já usam cache autenticado por revisão e não tiveram contrato alterado. Downloads de jogos têm prioridade sobre a mídia; download cancelado some da central. Vídeo remoto é pré-baixado e reproduzido localmente, sem streaming contínuo nem processamento em outras telas.
