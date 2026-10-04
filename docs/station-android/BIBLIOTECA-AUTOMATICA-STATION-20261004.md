# Biblioteca automática TurboStation

SNES, Mega Drive e Nintendo 64 usam o mesmo importador no HD de jogos. A pasta `n64` foi movida de `megadrive/n64` para a raiz, ao lado de `snes` e `megadrive`, preservando os 828 arquivos.

Produção de 04/10/2026: API `931030b`, catálogo **8 / 1.973 jogos**, 157 N64, 1.957 sinopses e 313 jogos com subpastas. [Retorno completo e evidências](RETORNO-SERVIDOR-N64-BIBLIOTECA-20261004.md).

## Acrescentar um jogo

1. Copie a ROM ou ZIP para a pasta da plataforma. N64 aceita `.z64/.n64/.v64/.rom`; SNES `.sfc/.smc/.swc/.fig`; Mega `.bin/.md/.gen/.smd`. ZIP precisa conter uma única ROM jogável desses formatos.
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

Sem revista, o jogo recebe uma imagem identificada como **CAPA PENDENTE**. Acrescentar depois a revista correta substitui essa imagem automaticamente. Capas ambíguas/inválidas e arquivos sem ROM única ficam no relatório de pendências. Os jogos já publicados continuam disponíveis. As revistas N64 são reduzidas para 480×720/JPEG qualidade 90, sem cortar a arte; os originais permanecem intactos.

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

N64 já é mapeado para `Nintendo 64`, pasta `nintendo-64`, motor `mupen64plus_next_gles3`. R8 instalado pode obter os 157 jogos com Atualizar. Consulta automática, sinopses do servidor e navegação R9 precisam da [integração de fontes publicada](https://github.com/luziellacerda/TurboElden/tree/ba669c27418341c7f232a644317881779ddd3bf3/versions/station-library-autodiscovery-20261004), seguida de assinatura/instalação no ambiente canônico. Esses passos ainda não ocorreram no Linux.

## Operação e retorno

Timer: `turborama-station-library-scan.timer`. Serviço: `turborama-station-library-scan.service`. Relatório de pendências: `/mnt/DADOS/turbostation-library-auto-20261004/report.json`; estado privado `state.json`.

Conteúdos ficam em objetos imutáveis por SHA256. O índice é substituído atomicamente; a API mantém o último válido se uma atualização falhar. Concessões abertas ficam ligadas ao arquivo/revisão anteriores durante sua validade.

O importador preserva jogos publicados quando a origem desaparece, inclusive ao desmontar o HD. As ROMs, capas originais e saves permanecem intactos. Um ZIP com arquivos auxiliares só é curado quando contém uma ROM única; esses arquivos extras não são instalados. A ROM alternativa de Ocarina of Time tem bytes diferentes do ZIP, ID próprio e revista exata; a extensão `.rom` foi acrescentada em dados e entrou automaticamente no catálogo.

Backup vigente: `/mnt/DADOS/station-folders-backup-20261004`. Retorno específico, quando necessário:

```text
python3 docs/station-android/scripts/implantar-pastas-station-20261004.py --rollback
```

Restaura somente os overrides próprios da API/importação à release 77d8d54, preservando N64, automação, online e conteúdo. A release anterior ignora folderPath. As guardas dos implantadores anteriores recusam a release atual. A publicação já foi concluída.

[Lista das 16 sinopses sem fonte](biblioteca-20261004/metadados-pendentes.tsv). A execução dos novos jogos no telefone e a partida entre dois aparelhos ainda precisam de prova. N64 online não foi habilitado; registro social permanece SNES/Mega.
