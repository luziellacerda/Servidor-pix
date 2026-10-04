# Biblioteca automática TurboStation

Escopo: SNES, Mega Drive e Nintendo 64 no HD de jogos. A pasta `n64` foi movida de `megadrive/n64` para a raiz, ao lado de `snes` e `megadrive`. Os 828 arquivos foram preservados, com o mesmo inode da pasta e dos arquivos. Os três diretórios passam pelo mesmo importador; nenhum jogo exige alteração de código.

## Acrescentar um jogo

1. Copie a ROM ou ZIP para a pasta da plataforma. Para N64: `.z64`, `.n64`, `.v64`, ou ZIP contendo uma única ROM desses formatos. SNES aceita `.sfc/.smc/.swc/.fig`; Mega `.bin/.md/.gen/.smd`.
2. Coloque a capa em `media/revista`, com **o mesmo nome do arquivo do jogo sem a extensão**. Subpastas de ROMs podem ter subpastas correspondentes na revista. Exemplo: `n64/Jogo.zip` e `n64/media/revista/Jogo.png`.
3. Se houver metadados, acrescente uma entrada em `gamelist.xml`, usando o caminho exato do jogo. O XML não é obrigatório para descobrir o arquivo.
4. Aguarde a cópia terminar. O servidor verifica a pasta a cada minuto, exige duas observações estáveis e publica somente após validar o conteúdo. A API verifica o índice a cada dez segundos. O cliente novo consulta o catálogo a cada minuto enquanto a tela estiver em primeiro plano e não houver download ativo. A descoberta normalmente leva de um a três minutos depois que a cópia termina; arquivos grandes também precisam terminar a verificação.

Sem capa, o novo jogo recebe uma imagem neutra identificada como **CAPA PENDENTE**. Colocar a revista correta depois substitui essa imagem automaticamente. Capas ambíguas ou inválidas e arquivos compactados sem uma ROM única ficam pendentes; a biblioteca já publicada continua disponível. As imagens são reduzidas para 480×720 sem cortar a arte, em JPEG qualidade90. Os originais do HD continuam intactos.

## Sinopse e outros dados

O servidor lê `desc`, `developer`, `publisher`, `genre`, `players` e `releasedate` do XML. Os jogos existentes também usam um complemento conciliado por ID, nome e plataforma com as fontes do aplicativo. Não se tenta adivinhar uma edição por título parecido.

Exemplo de entrada no XML:

```xml
<game>
  <path>./Jogo.zip</path>
  <name>Nome exibido</name>
  <desc>Sinopse desta edição.</desc>
  <developer>Desenvolvedora</developer>
  <publisher>Publicadora</publisher>
  <genre>Aventura</genre>
  <players>1-2</players>
  <releasedate>19980101T000000</releasedate>
</game>
```

Para corrigir dados sem alterar o XML, o arquivo privado `metadata-overrides.json` é um objeto indexado por `plataforma:caminho relativo`:

```json
{
  "n64:Jogo.zip": {
    "name": "Nome exibido",
    "description": "Sinopse desta edição.",
    "developer": "Desenvolvedora",
    "genre": "Aventura",
    "players": "1-2"
  }
}
```

Configuração privada: `/mnt/DADOS/station-library-auto-private-20261004/config.json`. Complementos manuais: `metadata-overrides.json` nessa mesma pasta. Para acrescentar uma **plataforma** suportada pelo aplicativo, registre uma pasta e suas extensões em `platforms` na configuração; não há cadastro individual de jogos no código. O registro de pasta não acrescenta um emulador ao APK. A variante BR é reconhecida pela subpasta `pt-br`.

## Como o aplicativo lê o servidor

Base: `https://app.lzgames.com.br`. Sempre use a sessão Station existente e verifique assinatura RSA-PSS, autoridade, produto, aplicativo, licença, aparelho e sessão.

| Requisição autenticada | Uso |
|---|---|
| `GET /v1/station/catalog` | Contrato anterior de cinco campos, compatível com R7/R8 |
| `GET /v1/station/catalog?metadata=1` | Mesmo domínio assinado `catalog/v1`; acrescenta `metadata` por item |
| `GET /v1/station/covers/{coverId}` | Imagem exata associada ao ID do catálogo |
| `POST /v1/station/downloads/authorize` | Solicitar por `itemId`; verificar revisão e descritor assinado |
| `GET /v1/station/artifacts/{grantId}` | Mesma sessão; concessão de uso único, válida por60s; validar tamanho/SHA256 antes de instalar |

`metadata` contém `description`, `developer`, `publisher`, `genre`, `players`, `releaseDate`. É opcional no cliente para permitir dados/cache anteriores. Os caminhos internos do HD e das releases não aparecem nas respostas. A revisão do catálogo muda quando o conteúdo publicado muda; a revisão de um jogo só muda para ROM/capa nova, preservando recibos de instalação e cache ao atualizar apenas sinopses. O texto tem limite2000caracteres e orçamento8KiB por item; descrição longa é limitada e paginada pelo cliente. A consulta com metadados tem limite64MiB no cliente; o contrato anterior permanece com o mesmo corpo de cinco campos.

N64 já é mapeado por R7/R8 para `Nintendo 64`, pasta local `nintendo-64`, motor `mupen64plus_next_gles3`. O APK instalado pode atualizar o catálogo manualmente para mostrar os156jogos. A consulta periódica e a leitura de sinopses do servidor exigem a atualização do código do cliente entregue nesta integração. Não exigem recompilar o APK a cada jogo.

## Operação, preservação e retorno

Timer próprio: `turborama-station-library-scan.timer`; serviço próprio de importação: `turborama-station-library-scan.service`. Relatório privado e pendências: `/mnt/DADOS/turbostation-library-auto-20261004/report.json`. Estado privado: `state.json`. Os arquivos do conteúdo ficam em objetos imutáveis identificados por SHA256. O índice é substituído por escrita atômica; a API mantém o último índice válido se uma atualização falhar. Concessões abertas continuam vinculadas ao arquivo e à revisão anteriores durante sua validade.

O importador não remove automaticamente jogos publicados quando a origem desaparece. Isso preserva downloads/instalações e evita perda de catálogo ao desmontar o HD. Nenhuma ROM, capa original, save, conta, chave ou migration é apagada. Um ZIP com vários arquivos é curado somente se contiver uma única ROM jogável; arquivos auxiliares não são instalados no Android. O `.rom` adicional de Ocarina of Time continua no HD e não foi tratado como BIOS: possui bytes diferentes do ZIP e extensão fora dos formatos N64 registrados.

Implantador próprio: `scripts/implantar-biblioteca-station-20261004.py`. Requer fonte limpa, manifesto completo, versão anterior real77d1dfb, índice4 original, backup restaurado, regressões com licenças sintéticas, UID real da API e HTTPS. O retorno `--rollback` desativa somente o timer/serviço/importação e o override desta implantação; devolve a API online77d1dfb e o índice4. Não move N64 de volta nem apaga os arquivos. Os implantadores anteriores foram superados e recusam a API nova.

Partida N64 online não é habilitada por esta importação. O registro social continua limitado aos dois motores SNES/Mega publicados. A homologação de partida em dois aparelhos continua pendente.
