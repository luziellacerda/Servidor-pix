# SERVIDOR → APP: N64, capas e biblioteca automática — 04/10/2026

O handoff online de04/10 foi concluído antes desta solicitação. O [retorno ONL-01 a ONL-07](RETORNO-SERVIDOR-ONLINE-STATION-20261004.md) registra a publicação das salas. Esta atualização mantém o módulo online e acrescenta N64, importação por pasta, metadados e troca de catálogo sem reiniciar a API.

## Estado atual comprovado

| Campo | Produção |
|---|---|
| Fonte da API | `77d8d5427941e2b0fc2a9f6a32e84aa71cc1fb7e` |
| Serviço | `turborama-station-api.service`, PID337611, UID995/GID981 |
| ExecStart | `/usr/bin/dotnet /opt/turborama-station-library-20261004-77d8d54/TurboRamaSuiteOnlineServer.dll` |
| SHA256 DLL | `14500ad850f41f6d361a8c29dd3a64ddd9b9642ad55133d14f814cbb0288e207` |
| Índice | `/mnt/DADOS/turbostation-library-auto-20261004/index.json` |
| Revisão atual | **7**; publicação inicial6, acréscimo automático posterior7 |
| SHA256 índice7 | `a9aaaf115604cf80f659348e5ef38b5ca7d153f552bf549a6e70398fe1bedb90` |
| Jogos visíveis | **1.973** |
| Compatibilidade oculta |255 IDs;2.228 entradas internas;996 IDs originais preservados |
| Sinopses com fonte |**1.957**;16 edições precisam de complemento manual |
| Capas públicas |Todas480×720; N64 JPEG derivados da revista exata |
| Importação |`turborama-station-library-scan.timer`, aproximadamente a cada minuto |
| Leitura pela API |`Station__LibraryAutoReload=true`, verificação a cada10 s |

| Plataforma | Jogos visíveis |
|---|---:|
| Super Nintendo |644|
| Super Nintendo BR |191|
| Mega Drive |887|
| Mega Drive BR |94|
| Nintendo 64 |**157**|
| Total |**1.973**|

O override específico é `zzzzz-station-library-20261004.conf`. Ele acrescenta **EnvironmentFile próprio**, posterior aos anteriores; o arquivo de ambiente original da5192 tinha precedência sobre atribuições simples `Environment=`. Essa divergência foi detectada na conferência HTTPS e houve retorno automático à API anterior antes da correção. A implantação final verifica o caminho efetivo do índice no processo. Nginx, arquivos de ambiente anteriores, chaves, banco/migrations e os12 PIDs compartilhados permanecem iguais ao início desta implantação. A configuração online, registro de motores, limites de transferência e quatro capas simultâneas foram preservados.

## N64: disco, arquivos e capas

- Pasta movida de `megadrive/n64` para a raiz do HD, ao lado de `snes` e `megadrive`. Renomeação no mesmo filesystem;828 arquivos/3.859.638.646bytes, inode e metadados preservados. A primeira inspeção encontrou a pasta em `snes`, mas, antes da operação, uma nova conferência localizou a origem efetiva em `megadrive`; nenhum caminho ausente foi movido por suposição.
-156 ZIPs disponíveis e156 revistas correspondentes. Cada ZIP tem uma ROM jogável única; CRC, nomes, segurança do arquivo, SHA256, tamanho e descritor foram conferidos. As57 entradas N64 do XML sem ROM não foram publicadas.
- As revistas N64 originais são PNG1024×1536,430.043.688bytes. Compiladas em480×720/JPEG90, preservando proporção e arte completa. Os originais continuam intactos; nenhum arquivo de `media/images` foi usado como substituto aproximado.
- A ROM solta de Ocarina of Time tem cabeçalho N64 e é diferente da ROM do ZIP. Não foi tratada como BIOS nem apagada. Foi incluída como **ROM alternativa**, com itemId próprio e capa de nome exato. Registrar `.rom` e o nome da edição em dados foi suficiente: o timer publicou a revisão7 e a API a leu mantendo o PID337611, iniciado12h28, antes da publicação7 às12h33.
- Os157 pares de ROM/revista estão no [catálogo N64 cruzado](biblioteca-20261004/n64-jogos-capas-downloads.tsv), incluindo SHA da imagem original e compilada, IDs, nome, arquivo/launchPath, formato, tamanho, hash e dados disponíveis. As157 ROMs transferidas preservam os bytes da origem. A ROM alternativa não tem entrada descritiva no XML; sua sinopse continua pendente.

## Catálogo completo e dados manuais

- [Todos os1.973 jogos visíveis por plataforma](biblioteca-20261004/catalogo-completo.tsv).
- [255 IDs ocultos de compatibilidade](biblioteca-20261004/compatibilidade-ids.tsv).
- [16 edições sem sinopse](biblioteca-20261004/metadados-pendentes.tsv).
- [Resumo e totais](biblioteca-20261004/resumo-catalogo.json).
- [Como acrescentar jogos, revista, XML e complementos](BIBLIOTECA-AUTOMATICA-STATION-20261004.md).

O importador descobre arquivos existentes mesmo sem entrada XML, usa nome de arquivo quando não há nome descritivo e imagem neutra **CAPA PENDENTE** quando ainda não há revista. Nunca associa a capa de título parecido. Ambiguidade, arquivo inválido e cópia ainda em andamento ficam no relatório privado de pendências. Copiar a revista correta depois atualiza a capa automaticamente. Adicionar jogos a SNES/Mega/N64 não exige editar programa ou recompilar o app. Uma plataforma adicional requer apenas registro de pasta/extensões em configuração e suporte real do motor no cliente.

## Contrato que o cliente deve consumir

Mesma base `https://app.lzgames.com.br`, TLS pin/authorityId atuais, sessão Station compartilhada. Não trocar assinatura/licença/appId.

1. Obter sessão pelo fluxo atual e `GET /v1/station/me` para o nome do comprador.
2. Buscar **`GET /v1/station/catalog?metadata=1`**. Verificar envelope RSA-PSS/contexto do catálogo e usar `itemId`, `name`, `platform`, `revision`, `coverId`, `metadata`. O domínio assinado continua `TurboRamaStationAndroid/catalog/v1`. `metadata` contém `description`, `developer`, `publisher`, `genre`, `players`, `releaseDate`.
3. Resolver plataforma pela tabela existente: `n64` → `Nintendo 64`/`nintendo-64`. Descobrir a lista pelo catálogo assinado; nenhum mapa compilado individual de jogos.
4. Pedir capa pelo **coverId dessa mesma entrada**: `GET /v1/station/covers/{coverId}` com bearer. Cache por ID/revisão, quatro workers, sem pausa após sucesso.
5. Autorizar por itemId em `POST /v1/station/downloads/authorize`; verificar a revisão e o descritor assinado. `GET /v1/station/artifacts/{grantId}` com a mesma sessão; validar tamanho/SHA256 e instalar o launchPath real. Concessão60 s, uso único, sem URL privada embutida no catálogo.
6. Consultar revisão nova periodicamente no foreground. Mesma revisão não deve cancelar fila/cache ou republicar o modelo. Mudança de sinopse aumenta a revisão do catálogo, preservando revisão do jogo/recibo/cache. Uma troca de ROM/capa aumenta a revisão do item; concessões já abertas permanecem vinculadas à edição anterior até expirar.

R7/R8 anteriores continuam usando `GET /v1/station/catalog`, com exatamente os cinco campos anteriores e sem metadata. Já reconhecem N64 após Atualizar/nova leitura. Os dados de sinopse do servidor e a consulta automática do cliente precisam da atualização inicial de fontes descrita abaixo. Catálogo/instalações não devem ser apagados para obter a lista nova.

## Fontes e compilação entregues ao aplicativo

Fonte do cliente **`c47cf152ae41018c41592796b55f18f8638a076c`**, baseR8 `ebd1199`. [Overlay, builder, contrato e limites do cliente](https://github.com/luziellacerda/TurboElden/tree/df6921a439375403d14be437cc049fb529f402de/versions/station-library-autodiscovery-20261004). Oito arquivos de runtime; UI/salasR8, `classes35.dex`, motores, assinatura, TLS, saves e dados preservados. Não sobrescrever essa fonte com uma preparação antigaR5/R7.

Java API 36/bytecode 8, DEX26+ e JNI arm64/16 KiB/libc++ estática compilados no Linux;404 checks Java nos grupos executados, paginação UTF-8 C++ e guardas/backup do overlay passaram. Renderer com o header novo teve sintaxe conferida sobreR8, usando headers de arte sintéticos **somente no teste**. Nenhum desses insumos de teste foi usado em APK.

**Não há APK novo assinado ou instalado nesta entrega Linux**: a base privadaR8, keystore original e telefone estão no fluxo canônicoWindows. Compilar o carousel com os insumos reais deE:, reempacotar sobreR8 apenas `classes28.dex`, `libstation_frontend.so`, `libturbo_carousel.so`, conferir todas as outras entradas porSHA e assinar com o mesmo certificado. Depois atualizar sobre o aplicativo, preservar dados e publicar recibo de assinatura/instalação. R7 é o último instalado comprovado. R8 é candidato compilado separado.

## Provas e desempenho

-11 checks de snapshot/metadata/concessão aberta/último índice válido; importador testado para jogo sem XML, capa pendente e posterior, mudança manual, ID estável, cópia incompleta, volume ausente, ROM/arquivo inseguro e ambiguidade.
-15 regressões da seleção revista;41 domínio+41 HTTP social; regressõesSuite passaram. PostgreSQL temporário confirmou92 checks online e ativação/sessão/perfil/catálogo/capas/descritor/ZIP/RAW/replay/revogação; metadata opcional não muda o corpo dos clientes anteriores.
- Release final sob UID995:92 checks, seis pares de capa/download (cinco plataformas+compatibilidade), assinaturas/metadata e remoção das licenças sintéticas. A mesma prova92 passou no HTTPS deprodução. Backup de API/configuração/índice restaurado por hash; dump PostgreSQL restaurado em cluster isolado, ledger028/029/030 conferido.
- Revisão7 real consumida por HTTPS sem reiniciar a API:1.973 jogos; metadata coincidente com o índice; ZIP N64 e ROM alternativa baixados com SHA correto.
- **157 capas N64 por HTTPS, quatro simultâneas:13.845,47ms,24.418.430bytes, zero divergência.** Esse teste usa Linux e rota pública; não mede o telefone/internet do usuário. Não há limite artificial de bytes/s nem espera entre capas bem-sucedidas. O original156PNGs soma430 MB; as157respostas compiladas somam24,4 MB.
- Estado final: timer ativo, online=true, reload=true,12 PIDs/configurações anteriores iguais, zero licenças `STATION_ROLLOUT_TEST` remanescentes.

[Recibo inicial6 e conferência pública7](biblioteca-20261004/evidencia-publicacao.json); [estado efetivo final](biblioteca-20261004/evidencia-estado-final.json).

## Retorno e pendências reais

Backup final: `/mnt/DADOS/station-library-auto-backup-20261004`. `scripts/implantar-biblioteca-station-20261004.py --rollback` remove somente o override e o timer/serviço desta importação e devolve a API online77d1dfb/catálogo4. Os arquivos N64 permanecem na raiz e os conteúdos novos são preservados. Não repetir `--apply`; a biblioteca já está em produção. Os scripts de implantação anteriores não devem ser usados contra esta API nova. Os recibos da tentativa devolvida foram arquivados, sem apagar a evidência.

Limites:16sinopses sem fonte; execução dos novos jogos no telefone ainda não conferida; novo APK do leitor automático não assinado/instalado aqui; partida direta em dois aparelhos continua pendente. A importação N64 não habilita N64 online: registro continua SNES/Mega. Após qualquer reinício da API, Reconectar na tela de salas obtém a instância social atual.
