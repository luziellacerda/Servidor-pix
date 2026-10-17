## Atualização da entrega: candidata R170 compilada

O backup foi recebido e a R170 assinada contém o atualizador e as correções PSX.
Leia [R170](../r170/README.md) e seu recibo: cadastro/deploy/instalação/piloto
ainda pendentes. A descrição e o recibo iniciais abaixo são históricos de preparo.

# Atualizações protegidas do aplicativo — 17/10/2026

**Implementação preparada e testada em código; não publicada no serviço nem
incluída em um APK de produção nesta entrega.** A primeira montagem depende
do backup privado da compilação atual e da assinatura original. Depois dessa
primeira instalação, as versões publicadas podem ser obtidas dentro do app.

## Fluxo para a pessoa

1. Ao abrir o catálogo, aparece **Atualização disponível**, com a versão.
2. **Atualizar** abre a tela de progresso e inicia a transferência protegida.
3. O app baixa, confere e prepara a atualização. Há cancelamento e mensagens
   para falta de espaço, permissão bloqueada e tentativa interrompida.
4. O Android apresenta sua confirmação de instalação. A atualização substitui
   o APK do mesmo pacote e assinatura, mantendo os dados do aplicativo.

O aviso usa a mesma Activity de catálogo já identificada pelo app:
`org.emulationstation.frontend.ESActivity`. Fica fora das telas dos emuladores
e de sala. A consulta ocorre após abrir o catálogo e depois a cada15min
enquanto essa tela está em primeiro plano. **Depois** adia o aviso por1h.
O download começa somente após o toque em Atualizar. O serviço em primeiro
plano mantém o trabalho e a notificação de progresso; a confirmação pendente
usa o Intent entregue pelo Android, inclusive na notificação.

Esta implementação solicita confirmação do sistema e mantém o SDK atual.
Não pressupõe root, Device Owner ou mudança de targetSdk para instalar sem
intervenção. O [contrato do PackageInstaller](https://developer.android.com/reference/android/content/pm/PackageInstaller.SessionParams)
define quando há ação do usuário; a [sessão de instalação](https://developer.android.com/reference/android/content/pm/PackageInstaller.Session)
entrega o resultado e a confirmação pendente. As [regras de assinatura](https://developer.android.com/studio/publish/app-signing)
exigem continuidade da identidade do aplicativo para uma atualização normal.

## Contrato HTTP

| Método e caminho | Função |
| --- | --- |
| POST `/v1/station/updates/check` | Manifesto assinado, vinculado a requestId/aparelho/licença/sessão; versão elegível ou `available=false`. |
| POST `/v1/station/updates/authorize` | Revalida a versão atual e emite grant de60s, com o descritor assinado do APK. |
| GET `/v1/station/updates/artifacts/{grantId}` | Consome uma autorização, com a mesma sessão e prova de requisição, e transmite o APK. |

Reutiliza **StationService.Session, StationRequestProof.Guard,
StationResponseSigner, StationGrantCipher e PostgresStationStore**. O mesmo
registro de grants existente recebe um item `update_…`, dentro do limite64.
Não há migração de banco. A criptografia tem domínio próprio e vincula
licença, dispositivo, sessionId, versão e arquivo imutável. O consumo é
atômico e de uso único, usando o SQL já existente.

Não há diretório estático, link público fixo, endpoint de upload de APK ou
caminho de arquivo enviado pelo cliente. O link isolado não basta: também
exige Bearer e a prova já negociada pelo aparelho. O prazo de60s vale para
**iniciar** a transferência; um download já autorizado pode terminar depois.
Resposta `no-store`, MIME APK, tamanho exato e redirects recusados. GET com
query/Range é recusado; após falha, uma nova tentativa obtém outro grant e
reinicia a transferência. Este primeiro protocolo não promete retomada parcial.

Corpo de8KiB; manifesto privado16KiB; APK até8GiB; ARM64/SDK26 ou superior; manter o SDK declarado pelo APK.
Limite por origem configurável em `Station:Updates:OriginRequestsPerMinute`
(padrão300, faixa30–3000). Não há limitador artificial de bytes/s nesta rota.

Os novos caminhos usam a **origem HTTPS e o pin atuais da API**. O seletor
R141 de downloads diretos permanece exato para os jogos. Não encaminhar o
novo GET ao host dedicado antes de qualificar sua regra de proxy: a regra
antiga só conhece `/v1/station/artifacts/…`.

## Validação do APK e publicação privada

O app calcula SHA256 durante a transferência, sem segunda leitura do download
novo. Confere pacote, versionCode maior e certificado igual ao instalado;
o instalador do Android faz a validação final da assinatura. Arquivo incompleto
não vira APK pronto. O cache é interno ao app; retentativas conferem o APK
retido, e os arquivos próprios antigos são removidos. Jogos, ROMs e suas
verificações de download não são alterados por esta ferramenta.

`ops/station-app-updates/publish.py` recebe o APK assinado e uma base confiável:

- Executa apksigner real: v2/v3 e o mesmo certificado da base.
- Exige pacote correto, ARM64, SDK, versionCode crescente e zipalign16KiB.
- Confere os componentes no Manifest e suas definições reais nas partições DEX.
- Compara os motores v3 anunciados pelo APK com o registro **efetivo** de
  perfis do Station. O limite17MiB acompanha o leitor atual; usar JSON compacto
  quando necessário, mantendo todos os registros anteriores.
- Confere os bytes ARM64 dos cores, runtimes e dependências declaradas pelos
  motores v3; descritores não substituem a leitura dessas bibliotecas.
- Copia e calcula o hash em uma passagem, detectando alteração da origem.
- Mantém APK imutável pelo SHA e troca `current.json` atomicamente sob lock.
- Rejeita symlinks e JSON duplicado. Diretórios750, APK440 e índice640;
  publicação como root exige o grupo de leitura do serviço.

O grupo do serviço conferido nesta preparação é `turborama-station-api`.
Diretório proposto, fora da raiz do site: `/mnt/DADOS/turbostation-app-updates`.
Esses caminhos são de operação; não aparecem no manifesto servido ao app.
A chave privada de assinatura Android não é necessária para publicar: o
publicador verifica o certificado que já está no APK assinado.

## Ordem de integração e liberação

1. Receber a base privada completa. Juntar esta alteração às correções PSX de
   inicialização/envio por evento publicadas em `ec2c693`. Aquelas correções
   também ainda precisam entrar no APK assinado. Preservar XML/CAS, controles,
   cores, opções, saves e demais componentes atuais.
2. No Git TurboElden, usar `integration/secure-app-updates`. Sete fontes novas;
   os três hooks são aplicados **aos fontes atuais recebidos** por
   `recipe/patch_sources.py`. Não substituir arquivos atuais por snapshots R123.
3. Congelar fontes e compilar com `recipe/compile_overlay.py`, usando o commit
   completo e classpath privado atual. O resultado é um **overlay**, com escopo
   em `SCOPED-TYPES.json`. Descobrir as partições da base e usar o assembler
   restrito existente. Não acrescentar um DEX com cópias das classes antigas.
   Todas as classes fora das famílias declaradas precisam permanecer exatas.
   `StationHttp$ResponseBody` também permanece exata, conforme o escopo R141.
4. Aplicar `recipe/merge_manifest.py` ao XML atual decodificado. Ele acrescenta
   as três declarações privadas e permissões necessárias, eleva versionCode e
   preserva SDK/factory/componentes anteriores. Reintegrar pelo empacotador
   privado preservando recursos e entradas. O XML não é um Manifest binário
   pronto para substituir dentro do ZIP.
5. Conferir APK completo, originalidade da assinatura, classes/partições,
   recursos, dados preservados e alinhamento. Regenerar fingerprints, owners,
   `engines.json` e o kit exato. Cadastrar no servidor **antes** de oferecer a
   atualização. A comparação do publicador é uma guarda adicional; não
   substitui a prova de que o serviço ativo recebeu os registros.
6. Publicar a nova DLL do Station pelo operador vigente, com baseline fresca,
   rollback compatível e ausência de salas/sessões para um eventual restart.
   Configurar `Station__Updates__Enabled=true` e
   `Station__Updates__Directory=/mnt/DADOS/turbostation-app-updates`.
   O padrão é desativado; sem versão publicada, a resposta assinada não oferece
   atualização. Sem novo perfil/aprovação de jogo por causa desta ferramenta.
7. Testar a primeira compilação em aparelho de teste: aviso, permissão inicial,
   download completo/cancelamento/retry, confirmação do Android, versão
   instalada, acesso e saves mantidos. Publicar só o APK efetivamente validado.

Exemplo do publicador **depois** dos passos anteriores e com caminhos reais:

```bash
python3 ops/station-app-updates/publish.py \
  --apk /CAMINHO_PRIVADO/final-assinado.apk \
  --trusted-apk /CAMINHO_PRIVADO/base-confiavel.apk \
  --registered-profiles /CAMINHO_EFETIVO/registro-perfis.json \
  --directory /mnt/DADOS/turbostation-app-updates \
  --read-group turborama-station-api \
  --apksigner /home/lz-servidor/android-sdk/build-tools/36.0.0/apksigner \
  --aapt2 /home/lz-servidor/android-sdk/build-tools/36.0.0/aapt2 \
  --zipalign /home/lz-servidor/android-sdk/build-tools/36.0.0/zipalign \
  --notes-file /CAMINHO_PRIVADO/novidades.txt
```

## Provas e limites desta entrega

- Servidor compila e passou80 verificações novas de protocolo/HTTP, assinatura,
  replay, expiração, identidade, concorrência, retirada da versão, symlinks e
  isolamento do caminho privado. O store de grants desse laboratório é sintético;
  o SQL real reutilizado e sua migração029 foram conferidos, sem migração nova.
- Regressões existentes Suite/Station de segurança, isolamento, extração e
  transferência passaram. Não foram usados banco/contas reais ou provedor.
- Java:43 verificações de assinatura/contexto do API real, staging, hash,
  cancelamento, corrupção, cache e allowlist. Compilação SDK e D8 isolados
  passaram; uma assinatura de método do painel foi fixture somente no classpath
  de teste, fora do módulo compilado. Não é o APK final nem teste da interface.
- Oito testes dos hooks/Manifest passaram, incluindo preservação do SDK,
  factory, componentes e recusa de regressão de versão.
- Publicador:13 verificações com apksigner/aapt2/D8/zipalign reais e APKs/chaves
  descartáveis sintéticos. Inclui assinatura estrangeira, módulo incompleto,
  motor não cadastrado, versão repetida e permissões de armazenamento inseguras.

**Não houve APK de produção compilado/assinado/instalado, publicação de APK
para usuários, mudança de DLL ativa, restart, rede, banco ou licenças.** Os APKs
do teste de publicação eram sintéticos e nunca foram instalados. O instalador
e a apresentação ainda exigem validação Android com a base privada atual.

Os limites e provas globais anteriores do Station permanecem válidos no seu
escopo, incluindo as pendências de gameplay PSX/N64. O atualizador não é
homologação desses jogos. O mesmo handoff consolidado acompanha a entrega.

Para corrigir uma versão já publicada, reconstruir a fonte estável com
versionCode maior e a assinatura original. O cliente recusa regressão de versão.
