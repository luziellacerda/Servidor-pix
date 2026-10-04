# SERVIDOR → APP: N64, capas e biblioteca automática — 04/10/2026

O handoff online de 04/10 foi concluído primeiro. O [retorno ONL-01 a ONL-07](RETORNO-SERVIDOR-ONLINE-STATION-20261004.md) registra a publicação das salas. A entrega atual preserva esse módulo e acrescenta N64, descoberta por pasta, sinopses e subpastas R9. As alterações do catálogo passam a ser carregadas sem reiniciar a API.

## Estado atual comprovado

| Campo | Produção |
|---|---|
| Fonte da API | `931030bba25ca8a783f096b72dcecd26a7b49387` |
| Serviço | `turborama-station-api.service`, PID 347227, UID 995/GID 981 |
| ExecStart | `/usr/bin/dotnet /opt/turborama-station-folders-20261004-931030b/TurboRamaSuiteOnlineServer.dll` |
| SHA256 DLL | `0b3f5da385216d216fb55220789f55c40b8eb304b7b1a4759cc154b1aa3f3ab0` |
| Índice | `/mnt/DADOS/turbostation-library-auto-20261004/index.json` |
| Revisão atual | **8**: publicação N64 inicial 6, ROM alternativa automática 7, subpastas R9 8 |
| SHA256 índice 8 | `f56cf70267251ade518ae111be88627db83d592436b7fb3bf522c119e8fefb8c` |
| Jogos visíveis | **1.973** |
| Compatibilidade oculta | 255 IDs; 2.228 entradas internas; 996 IDs originais preservados |
| Sinopses com fonte | **1.957**; 16 edições precisam de complemento manual |
| Hierarquia | `folderPath` publicado nos dois contratos; 313 jogos em subpastas |
| Capas públicas | Todas 480×720; N64 JPEG derivados da revista exata |
| Importação | `turborama-station-library-scan.timer`, aproximadamente a cada minuto |
| Leitura pela API | `Station__LibraryAutoReload=true`, verificação a cada 10 segundos |

| Plataforma | Jogos visíveis |
|---|---:|
| Super Nintendo | 644 |
| Super Nintendo BR | 191 |
| Mega Drive | 887 |
| Mega Drive BR | 94 |
| Nintendo 64 | **157** |
| Total | **1.973** |

O override específico é `zzzzz-station-library-20261004.conf`, com EnvironmentFile próprio posterior aos anteriores. O arquivo de ambiente original da 5192 tinha precedência sobre atribuições simples `Environment=`; a primeira tentativa foi devolvida automaticamente e essa configuração foi corrigida antes da publicação bem-sucedida. O caminho efetivo do índice foi conferido no processo. Nginx, configurações anteriores, chaves, migrations e os 12 PIDs compartilhados permanecem iguais. Online, registro dos motores e limites de transferência foram preservados. O pin TLS atual também foi conferido e permanece igual.

## N64: disco, arquivos e capas

- Pasta movida de `megadrive/n64` para a raiz do HD, ao lado de `snes` e `megadrive`. Renomeação no mesmo filesystem: 828 arquivos/3.859.638.646 bytes, inode e metadados preservados. A origem efetiva foi conferida imediatamente antes da operação.
- 156 ZIPs e 156 revistas correspondentes. Cada ZIP tem uma ROM jogável única; CRC, nomes, segurança do arquivo, SHA256, tamanho e descritor conferidos. As 57 entradas N64 do XML sem ROM foram excluídas da publicação.
- Revistas N64 originais PNG 1024×1536: 430.043.688 bytes. Compiladas em 480×720/JPEG qualidade 90, preservando proporção e arte completa. Os originais estão intactos; nenhuma capa aproximada de `media/images` foi usada.
- A ROM solta de Ocarina of Time tem cabeçalho N64 e bytes diferentes da ROM do ZIP. Foi preservada e catalogada como **ROM alternativa**, com ID próprio e capa de nome exato. Registrar `.rom` e o nome da edição em dados foi suficiente: o timer publicou a revisão 7, lida pela API sem reinício, mantendo o PID 337611 anterior. A publicação do código R9 depois precisou de uma nova release da API, agora PID 347227.
- [157 pares de ROM/revista conferidos](biblioteca-20261004/n64-jogos-capas-downloads.tsv): SHA original/compilado, IDs, nomes, pasta, arquivo, launchPath, formato, tamanho e dados disponíveis. Todas as ROMs transferidas preservam os bytes da origem.

## Catálogo completo e dados manuais

- [Todos os 1.973 jogos por plataforma](biblioteca-20261004/catalogo-completo.tsv).
- [255 IDs ocultos de compatibilidade](biblioteca-20261004/compatibilidade-ids.tsv).
- [16 edições sem sinopse](biblioteca-20261004/metadados-pendentes.tsv).
- [Resumo e totais](biblioteca-20261004/resumo-catalogo.json).
- [Como acrescentar jogos, revista, XML e complementos](BIBLIOTECA-AUTOMATICA-STATION-20261004.md).

O importador descobre ROMs mesmo sem XML, usa o nome do arquivo quando faltam dados e uma imagem identificada como **CAPA PENDENTE** quando falta revista. Capas ambíguas, arquivos inválidos e cópias em andamento ficam no relatório de pendências. Acrescentar a revista correta depois atualiza a capa automaticamente. Novos jogos em SNES/Mega/N64 não exigem editar o programa nem recompilar o app. Uma plataforma adicional exige registro de pasta/extensões e suporte real do motor no cliente.

## Contrato que o cliente deve consumir

Base `https://app.lzgames.com.br`, pin/authorityId e sessão Station existentes.

1. Obter sessão pelo fluxo atual; `GET /v1/station/me` fornece o nome do comprador.
2. Buscar **`GET /v1/station/catalog?metadata=1`**. Verificar RSA-PSS e contexto do envelope `TurboRamaStationAndroid/catalog/v1`. Cada item conserva `itemId`, `name`, `platform`, `revision`, `coverId` e acrescenta `folderPath` e `metadata`. Metadata contém `description`, `developer`, `publisher`, `genre`, `players`, `releaseDate`.
3. Usar `folderPath` somente para apresentação. É um array de até 8 segmentos, cada um até 80 unidades UTF-16; `[]` significa raiz. Deriva da subpasta real da ROM, sem caminho absoluto ou função de destino de instalação.
4. Resolver a plataforma pelo mapeamento existente: `n64` → `Nintendo 64`/`nintendo-64`. Obter a lista do catálogo assinado.
5. Pedir a capa pelo **coverId da mesma entrada**, em `GET /v1/station/covers/{coverId}`, com bearer. Cache por ID/revisão e quatro workers, reutilizando cada vaga após concluir uma imagem.
6. Autorizar por itemId em `POST /v1/station/downloads/authorize`; verificar revisão e descritor assinado. Baixar em `GET /v1/station/artifacts/{grantId}` com a mesma sessão, conferir tamanho/SHA256 e instalar o launchPath real. Concessão de 60 segundos e uso único.
7. Consultar novas revisões em primeiro plano. Uma revisão igual não deve republicar o modelo nem cancelar a fila/cache. Alterar somente metadata/pastas aumenta a revisão do catálogo; ROM/capa nova aumenta a revisão do jogo. Concessões abertas continuam ligadas à edição anterior até expirar.

A consulta sem `?metadata=1` preserva os cinco campos obrigatórios anteriores e também envia `folderPath`, extensão ignorada por R7/R8. Os clientes anteriores reconhecem N64 após Atualizar/nova leitura. Sinopses do servidor, consulta periódica e navegação R9 exigem a atualização inicial do app. O catálogo e as instalações existentes podem ser preservados.

## Fontes e compilação entregues ao aplicativo

Cliente **`ba669c27418341c7f232a644317881779ddd3bf3`**, conciliado com R9 `a325e69`. [Overlay, builder e instruções de compilação](https://github.com/luziellacerda/TurboElden/tree/ba669c27418341c7f232a644317881779ddd3bf3/versions/station-library-autodiscovery-20261004). Oito arquivos de runtime com guardas SHA e backup; salas, botões e navegação R9 preservados. Java e JNI precisam ser atualizados juntos: publicação com seis colunas legadas, sétima de pasta e oitava de sinopse, mantendo ABI 0xe8.

**433 verificações Java**, 36 C++ de coleções e 429 C++ de navegação passaram, além de paginação UTF-8 e aplicação/restauração do overlay. Java API 36/bytecode 8, DEX26+ e JNI arm64/16 KiB/libc++ estática compilados. A sintaxe do renderer R9 foi conferida usando imagens sintéticas somente no teste.

**Esta integração não gerou nem instalou APK assinado no Linux. R8 é o último instalado comprovado; o APK R9 anterior está compilado/assinado no Windows e ainda sem instalação.** Aplicar o overlay à fonte canônica R9 em `E:\ESTUDO APK\work\station-netplay-20261004`. Recompilar o carousel com os insumos reais, reempacotar a base R9 alterando somente `classes28.dex`, `libstation_frontend.so` e `libturbo_carousel.so`, preservar `classes35.dex` das salas e as demais entradas por SHA256. Assinar com o certificado original, instalar por atualização e publicar recibo. Ao reconectar a USB, restaurar `stay_on_while_plugged_in=0`, pendência registrada no retorno R9.

## Provas e desempenho

- 23 verificações C# de snapshot, metadata, pastas, concessões abertas e último índice válido. Importador testado para ROM sem XML, capa pendente/posterior, correção manual, ID estável, cópia incompleta, volume ausente, arquivo inseguro e ambiguidade.
- 15 regressões da seleção revista; 41 domínio e 41 HTTP social; regressões Suite concluídas. PostgreSQL isolado confirmou 92 verificações online e os fluxos de ativação, sessão, perfil, catálogo, capas, descritores, ZIP/RAW, replay e revogação.
- Release R9 sob UID 995: **92 verificações candidato + 92 HTTPS em produção**, seis pares capa/download, assinaturas, metadata, folderPath e limpeza das licenças sintéticas. Backup de API/configuração/índice restaurado por hash; dump PostgreSQL restaurado em cluster isolado e ledger 028/029/030 conferido.
- Revisão 7 real recebida por HTTPS sem reiniciar a API; ZIP N64 e ROM alternativa baixados com SHA correto. A revisão 8 mantém os mesmos conteúdos e acrescenta a hierarquia verificada por HTTPS.
- **157 capas N64 por HTTPS, quatro simultâneas: 13.845,47 ms, 24.418.430 bytes, zero divergências.** Medição do Linux na rota pública; o tempo no telefone depende da conexão. Os 156 PNGs originais somam 430 MB; as 157 respostas compiladas somam 24,4 MB. A API não introduz limite de bytes por segundo; conserva os limites de quantidade de requisições.
- Estado final: timer, online e reload ativos; 12 PIDs e configurações compartilhadas preservados; zero licenças sintéticas remanescentes.

[Recibos e auditoria de capas](biblioteca-20261004/evidencia-publicacao.json); [publicação R9/pastas](biblioteca-20261004/evidencia-pastas-r9.json); [estado efetivo final](biblioteca-20261004/evidencia-estado-final.json).

## Retorno e pendências reais

Backup vigente: `/mnt/DADOS/station-folders-backup-20261004`. Retorno específico:

```text
python3 docs/station-android/scripts/implantar-pastas-station-20261004.py --rollback
```

Esse retorno restaura somente os overrides próprios da API/importação à release 77d8d54, preservando N64, importação automática, online e os conteúdos já publicados. O leitor anterior ignora folderPath. Os implantadores anteriores foram superados e suas guardas recusam a release atual. A implantação já foi aplicada.

Restam 16 sinopses sem fonte, incorporação/assinatura/instalação do novo APK, prova de execução dos jogos no telefone e partida entre dois aparelhos. N64 online não foi habilitado: o registro permanece SNES/Mega. Depois de reiniciar a API, Reconectar na tela de salas obtém a nova instância social.
