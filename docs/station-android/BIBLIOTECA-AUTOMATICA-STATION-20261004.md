# Biblioteca automática TurboStation

SNES, Mega Drive, Nintendo 64 e Neo Geo usam o mesmo importador no HD de jogos. A pasta `n64` foi movida de `megadrive/n64` para a raiz, ao lado de `snes` e `megadrive`, preservando os 828 arquivos.

Produção de05/10/2026: API `931030b` mantida, scanner `cb49214`, catálogo **9 / 2.162 jogos**,189Neo Geo,157 N64,2.119 sinopses e374 jogos com subpastas. Neo Geo foi movido de SNES para a raiz,826 arquivos intactos. [Retorno completo e evidências](RETORNO-SERVIDOR-NEOGEO-VELOCIDADE-20261005.md).

## Acrescentar um jogo

1. Copie a ROM ou ZIP para a pasta da plataforma. N64 aceita `.z64/.n64/.v64/.rom`; SNES `.sfc/.smc/.swc/.fig`; Mega `.bin/.md/.gen/.smd`. Nessas três plataformas, ZIP precisa conter uma única ROM jogável desses formatos. **Neo Geo aceita o ZIP fechado do jogo**, mantendo `neogeo.zip` íntegro na raiz da plataforma; o pacote entrega ambos e abre o ZIP do jogo, sem extrair chips. A BIOS não aparece como jogo.
2. Coloque a capa em `media/revista`, com **o mesmo nome do arquivo do jogo sem a extensão**.
3. Se houver dados, acrescente uma entrada em `gamelist.xml` usando o caminho exato. XML é opcional para descobrir o arquivo.
4. Aguarde a cópia terminar. O importador verifica cada minuto e exige duas observações estáveis. A API lê o índice a cada 10 segundos. O cliente atualizado consulta a cada minuto em primeiro plano, autorizado e sem download ativo.

A descoberta normalmente leva de um a três minutos após terminar a cópia, mais o tempo de validação de arquivos grandes. O novo cliente é necessário uma vez para consulta automática e sinopses do servidor; jogos posteriores dispensam recompilação.

Exemplo com subpasta:

```text
n64/Aventura/Jogo.zip
n64/media/revista/Aventura/Jogo.png
```

Esse jogo recebe `folderPath: ["Aventura"]`. Uma estrutura com `Aventura/Selecionados` gera dois segmentos. A hierarquia deriva dos jogos disponíveis, por plataforma. `folderPath` organiza a tela e não determina a pasta de instalação do Android.

Sem revista, o jogo recebe uma imagem identificada como **CAPA PENDENTE**. Acrescentar depois a revista correta substitui essa imagem automaticamente. Capas ambíguas/inválidas e arquivos sem ROM única ficam no relatório de pendências. Os jogos já publicados continuam disponíveis. As revistas N64 e Neo Geo são reduzidas para480×720/JPEG qualidade90, sem cortar a arte; os originais permanecem intactos. ZIP Neo Geo com CRC inválido fica pendente. O importador só recompõe BIOS quando existe companion válido com o mesmo nome/tamanho/CRC do membro; o original é preservado. Art of Fighting 2/aof2.zip continua pendente por chip gráfico corrompido, sem cópia válida local.

## Sinopse e outros dados

O servidor lê `desc`, `developer`, `publisher`, `genre`, `players` e `releasedate` do XML. Os jogos existentes também usam um complemento conciliado por ID, nome e plataforma com as fontes do app. Uma edição não é associada apenas pela semelhança do título.

```xml
<game>
  <path>./Aventura/Jogo.zip</path>
  <name>Nome exibido</name>
  <desc>Sinopse desta edição.</desc>
  <developer>Desenvolvedora</developer>
  <publisher>Publicadora</publisher>
  <genre>Aventura</genre>
  <players>1-2</players>
  <releasedate>19980101T000000</releasedate>
</game>
```

Para corrigir dados sem alterar o XML, `metadata-overrides.json` usa `plataforma:caminho relativo`:

```json
{
  "n64:Aventura/Jogo.zip": {
    "name": "Nome exibido",
    "description": "Sinopse desta edição.",
    "developer": "Desenvolvedora",
    "genre": "Aventura",
    "players": "1-2"
  }
}
```

Configuração privada: `/mnt/DADOS/station-library-auto-private-20261004/config.json`. O arquivo `metadata-overrides.json` fica na mesma pasta. Ambos são de operação do servidor; não entram no APK ou no catálogo público.

Para uma plataforma adicional suportada pelo app, registre sua pasta e extensões em `platforms` da configuração. Esse registro não acrescenta um emulador ao APK. A variante BR é reconhecida pela subpasta `pt-br`.

## Como o aplicativo lê o servidor

Base: `https://app.lzgames.com.br`. Usar a sessão Station existente e verificar RSA-PSS, autoridade, produto, aplicativo, licença, aparelho e sessão.

| Requisição autenticada | Uso |
|---|---|
| `GET /v1/station/catalog` | Cinco campos obrigatórios anteriores e `folderPath`; clientes antigos ignoram a extensão |
| `GET /v1/station/catalog?metadata=1` | Mesmo domínio assinado `catalog/v1`; inclui `metadata` por item |
| `GET /v1/station/covers/{coverId}` | Capa exata associada à entrada do catálogo |
| `POST /v1/station/downloads/authorize` | Autorizar por `itemId`; verificar revisão e descritor assinado |
| `GET /v1/station/artifacts/{grantId}` | Mesma sessão; concessão de 60 segundos/uso único; conferir tamanho/SHA256 antes de instalar |

Cada item conserva `itemId`, `name`, `platform`, `revision` e `coverId`. `folderPath` é array, `[]` para raiz, até 8 níveis e 80 unidades UTF-16 por segmento. São recusados nomes vazios, controles, barras, `.`/`..` e Unicode inválido.

`metadata` contém `description`, `developer`, `publisher`, `genre`, `players`, `releaseDate`. É opcional no cliente para permitir dados/cache anteriores. Caminhos internos e URLs privadas não aparecem nas respostas. Descrição tem até 2.000 caracteres e orçamento de 8 KiB por item; o cliente consulta com limite de 64 MiB e pagina o texto respeitando palavras/UTF-8.

Metadata/pastas novas alteram a revisão do catálogo, preservando revisão individual, recibos e cache. ROM/capa nova aumenta a revisão do item. Uma revisão de catálogo igual não deve reiniciar a fila de capas. Cache usa coverId/revisão; quatro vagas são reutilizadas imediatamente após cada imagem terminar. Downloads mantêm tamanho/SHA e o descritor launchPath real.

N64 já é mapeado para `Nintendo 64`, pasta `nintendo-64`, motor `mupen64plus_next_gles3`; Neo Geo também já tem mapeamento/motor no app. R9 é o último instalado comprovado; R10/R11 contêm consulta automática e sinopses, mas R11 ainda aguarda USB/instalação. A fonte [c8e240a](https://github.com/luziellacerda/TurboElden/tree/c8e240a2c886122e79ca2105c0a719a9217ed7dc/versions/station-neogeo-rate-20261005) acrescenta somente MB/s sobreR11. Não repetir o overlay antigo N64 sobre as fontesR10/R11. Nenhum APK foi assinado/instalado neste Linux; execução dos novos jogos no aparelho ainda precisa de prova.

## Velocidade de todos os jogos

Todas as plataformas publicadas usam a mesma rota de artefatos, sem limite artificial de MB/s por jogo. O download usa a banda disponível entre servidor e aparelho. O contador em passos de 1 MB mostra bytes acumulados, não a taxa. A fonte nova do app mostra MB/s real durante a transferência; precisa entrar no próximo APK. [Medições e evidências](RETORNO-SERVIDOR-NEOGEO-VELOCIDADE-20261005.md).

## Operação e retorno

Timer: `turborama-station-library-scan.timer`. Serviço: `turborama-station-library-scan.service`. Relatório de pendências: `/mnt/DADOS/turbostation-library-auto-20261004/report.json`; estado privado `state.json`.

Conteúdos ficam em objetos imutáveis por SHA256. O índice é substituído atomicamente; a API mantém o último válido se uma atualização falhar. Concessões abertas ficam ligadas ao arquivo/revisão anteriores durante sua validade.

O importador preserva jogos publicados quando a origem desaparece, inclusive ao desmontar o HD. As ROMs, capas originais e saves permanecem intactos. Nos modos de ROM única SNES/Mega/N64, um ZIP com arquivos auxiliares só é curado quando contém uma ROM única; esses arquivos extras não são instalados. Neo Geo usa o modo separado `arcade-set`, mantendo ZIP fechado e BIOS no pacote externo, com `fileCount=2` e `launchPath=<jogo>.zip`. A ROM alternativa de Ocarina of Time tem bytes diferentes do ZIP, ID próprio e revista exata; a extensão `.rom` foi acrescentada em dados e entrou automaticamente no catálogo.

Backup vigente: `/mnt/DADOS/station-neogeo-backup-20261005`, com índice8/estado/configuração/unidade próprios anteriores restaurados porSHA. Retorno específico, quando necessário:

```text
python3 docs/station-android/scripts/implantar-neogeo-station-20261005.py --rollback
```

Restaura somente o scanner/configuração/estado próprios anteriores e republica o conteúdo anterior com revisão crescente9→10, mantendo API, HD e novos arquivos intactos. Recusa sobrescrever índice que já tenha avançado com outros jogos. Os rollbacks de04/10 são históricos e não servem para desfazer esta publicação. Não repetir --apply após sucesso.

[Lista atual das43sinopses sem fonte](biblioteca-20261005/metadados-pendentes.tsv). Art of Fighting 2 está pendente por ZIP corrompido; substituir a origem por cópia válida permitirá importação automática. Execução dos novos jogos no telefone e partida entre dois aparelhos ainda precisam de prova. N64/Neo Geo online não foram habilitados; registro social permanece SNES/Mega.
