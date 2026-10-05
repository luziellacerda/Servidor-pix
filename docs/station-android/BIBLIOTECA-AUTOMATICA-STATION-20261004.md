# Biblioteca automática TurboStation

SNES, Mega Drive, Nintendo 64, Neo Geo e Neo Geo CD usam o mesmo importador no HD de jogos. A pasta `n64` foi movida de `megadrive/n64` para a raiz, ao lado de `snes` e `megadrive`, preservando os 828 arquivos.

Produção de 05/10/2026: API `931030b` mantida, scanner `9cff9b3`, catálogo **14 / 2.212 jogos**, 50 Neo Geo CD, 189 Neo Geo, 157 N64, 2.169 sinopses e 374 jogos com subpastas. Neo Geo CD permanece em `neogeo/neogeocd`, conforme solicitado. [Retorno e catálogo completo](RETORNO-SERVIDOR-NEOGEOCD-20261005.md).

## Acrescentar um jogo

1. Copie a ROM ou ZIP para a pasta da plataforma. N64 aceita `.z64/.n64/.v64/.rom`; SNES `.sfc/.smc/.swc/.fig`; Mega `.bin/.md/.gen/.smd`. Nessas três plataformas, ZIP precisa conter uma única ROM jogável desses formatos. **Neo Geo aceita o ZIP fechado do jogo**, mantendo `neogeo.zip` íntegro na raiz da plataforma; o pacote entrega ambos e abre o ZIP do jogo, sem extrair chips. A BIOS não aparece como jogo. **Neo Geo CD recebe `.chd` ou `.img` que já contenha CHDv5**, em `neogeo/neogeocd`; a entrega usa extensão `.chd` e bytes idênticos. Não renomear uma imagem RAW/CUE/BIN para CHD: esses formatos precisam de preparação própria.
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

Sem revista, o jogo recebe uma imagem identificada como **CAPA PENDENTE**. Acrescentar depois a revista correta substitui essa imagem automaticamente. Capas ambíguas/inválidas e arquivos sem ROM única ficam no relatório de pendências. Os jogos já publicados continuam disponíveis. As revistas N64, Neo Geo e Neo Geo CD são reduzidas para480×720/JPEG qualidade90, sem cortar a arte; os originais permanecem intactos. ZIP Neo Geo com CRC inválido fica pendente. O importador só recompõe BIOS quando existe companion válido com o mesmo nome/tamanho/CRC do membro; o original é preservado. Art of Fighting 2/aof2.zip continua pendente por chip gráfico corrompido, sem cópia válida local.

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
| `GET /v1/station/artifacts/{grantId}` | Mesma sessão; concessão de 60 segundos/uso único; conferir descritor/tamanho e instalar conforme o fluxo R16 |

Cada item conserva `itemId`, `name`, `platform`, `revision` e `coverId`. `folderPath` é array, `[]` para raiz, até 8 níveis e 80 unidades UTF-16 por segmento. São recusados nomes vazios, controles, barras, `.`/`..` e Unicode inválido.

`metadata` contém `description`, `developer`, `publisher`, `genre`, `players`, `releaseDate`. É opcional no cliente para permitir dados/cache anteriores. Caminhos internos e URLs privadas não aparecem nas respostas. Descrição tem até 2.000 caracteres e orçamento de 8 KiB por item; o cliente consulta com limite de 64 MiB e pagina o texto respeitando palavras/UTF-8.

Metadata/pastas novas alteram a revisão do catálogo, preservando revisão individual, recibos e cache. ROM/capa nova aumenta a revisão do item. Uma revisão de catálogo igual não deve reiniciar a fila de capas. Cache usa coverId/revisão; quatro vagas são reutilizadas imediatamente após cada imagem terminar. Downloads conservam tamanho/SHA esperado no descritor assinado e launchPath real; R16 não calcula SHA do corpo dos jogos.

O cliente R18 instalado já mapeia N64, Neo Geo e `neogeocd` → **Neo Geo CD**, pasta `neo-geo-cd`. Preserva consulta automática/metadados, navegação R17 e desempenho/offline R16. [Retorno cliente/CD](https://github.com/luziellacerda/TurboElden/tree/5dea14c8b361ec6f0a8fb6c8a1ac92720d27542a/versions/station-neogeocd-20261005) prepara a correção do lançamento CD/importação BIOS sobre R18; Java/DEX compilado, APK ainda pendente. O novo catálogo/download CD está em produção. Não retroceder para R15/R11 ou executar seus empacotadores antigos.

## BIOS Neo Geo CD

O usuário não possui BIOS CD. Catálogo, capas, sinopses e downloads permanecem disponíveis. O novo app deverá explicar a falta e oferecer **IMPORTAR BIOS**, pelo seletor normal Android, preparando os componentes sem ADB ou cópia manual no telefone.

No servidor, uma BIOS própria pode ser colocada em `neogeo/neogeocd/bios`, como `neocd.zip`, `neocdz.zip`, `.bin` ou `.rom`. O importador valida identidades exatas para CDZ. Requer firmware CD e auxiliar `000-lo.lo`; a BIOS Neo Geo MVS fornece o auxiliar, mas não substitui o firmware. Quando houver BIOS válida, o pacote é recomposto automaticamente com CHD + `neocdz.zip` fechado, mantendo IDs e aumentando revisão. Enquanto faltar, entrega CHD RAW sem BIOS; a pendência aparece em `runtimeRequirements` do relatório privado.

A próxima compilação do cliente deve preservar o modo filesystem R18: `PREF_ROMsDIR_2=""` e `-rompath '<pai real>'`. O disco vai em `-cdrom '<CHD absoluto>'`, driver `neocdz`; aspas simples. Não preencher ROMsDIR com o caminho físico, que ativaria SAF sem URI.

## Velocidade de todos os jogos

Todas as plataformas publicadas usam a mesma rota de artefatos, sem limite artificial de MB/s por jogo. O download usa a banda disponível entre servidor e aparelho. O contador em passos de 1 MB mostra bytes acumulados, não a taxa. A R16/R18 já mostra MB/s real durante a transferência, separa preparação e preserva a fila sem pacing. Por pedido do usuário, R16 retirou SHA do corpo dos jogos; metadados assinados/tamanhos/paths e hash de netplay/build foram preservados. [Medições e evidências](RETORNO-SERVIDOR-NEOGEO-VELOCIDADE-20261005.md).

## Operação e retorno

Timer: `turborama-station-library-scan.timer`. Serviço: `turborama-station-library-scan.service`. Relatório de pendências: `/mnt/DADOS/turbostation-library-auto-20261004/report.json`; estado privado `state.json`.

Conteúdos ficam em objetos imutáveis por SHA256. O índice é substituído atomicamente; a API mantém o último válido se uma atualização falhar. Concessões abertas ficam ligadas ao arquivo/revisão anteriores durante sua validade.

O importador preserva jogos publicados quando a origem desaparece, inclusive ao desmontar o HD. As ROMs, capas originais e saves permanecem intactos. Nos modos de ROM única SNES/Mega/N64, um ZIP com arquivos auxiliares só é curado quando contém uma ROM única; esses arquivos extras não são instalados. Neo Geo usa o modo separado `arcade-set`, mantendo ZIP fechado e BIOS no pacote externo, com `fileCount=2` e `launchPath=<jogo>.zip`. A ROM alternativa de Ocarina of Time tem bytes diferentes do ZIP, ID próprio e revista exata; a extensão `.rom` foi acrescentada em dados e entrou automaticamente no catálogo.

Backup vigente: `/mnt/DADOS/station-neogeocd-backup-20261005`, com índice13/estado/configuração/unidade próprios anteriores restaurados porSHA. Retorno específico, quando necessário:

```text
python3 docs/station-android/scripts/implantar-neogeocd-station-20261005.py --rollback
```

Restaura somente o scanner/configuração/estado próprios anteriores e republica o conteúdo anterior com revisão crescente14→15, mantendo API, HD e novos arquivos intactos. Recusa sobrescrever índice que já tenha avançado com outros jogos. Os rollbacks de04/10 são históricos e não servem para desfazer esta publicação. Não repetir --apply após sucesso.

[Lista atual das43sinopses sem fonte](biblioteca-neogeocd-20261005/metadados-pendentes.tsv). Art of Fighting 2 está pendente por ZIP corrompido; substituir a origem por cópia válida permitirá importação automática. NeoCD/N64, retorno e partida entre dois aparelhos ainda precisam de prova; SVC Plus e controles já foram observados na R18. N64/Neo Geo online não foram habilitados; registro social permanece SNES/Mega.
