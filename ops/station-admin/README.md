# Cadastro de clientes publicado —06/10/2026

[Operação e implementação do cadastro](REGISTRATION.md). A página Station agora oferece **Novo cliente e código**, cliente novo/existente, venda/cortesia/teste e licença adicional para outro celular. Administraçãofe4b631 e retorno em `docs/station-android/RETORNO-CADASTRO-CLIENTES-STATION-20261006.md`. Use o publicador/rollback específico `deploy-registration.py`; a implantação03/10 abaixo é histórica.

# Administração do Station pelo site

Alvo: `https://turbobox.lzgames.com.br/admin/station`, dentro da administração existente.
A página exige a conta administrativa do site. Não usa senha sudo.

## Atendimento para o operador

1. Entre na administração e abra **Códigos Station** no menu, ou **Gerar código do app Station** na visão geral.
2. Busque pelo nome do cliente, número do pedido ou identificação da licença.
3. Para ativação inicial/código vencido, clique em **Gerar código** na linha do cliente. Para um aparelho já ativado, **Trocar celular** gera o código de substituição. **Abrir atendimento** continua oferecendo reinstalação, bloqueio, desbloqueio e histórico.
4. Confira a licença e o efeito da ação, registre o motivo e confirme com sua senha administrativa do site.
5. Quando houver código, copie e entregue por canal privado. Ele vale **30 minutos**, é de uso único e desaparece da página ao fechar ou atualizar.

| Situação | Ação no painel | Resultado |
| --- | --- | --- |
| Primeiro acesso, código vencido, perdido ou necessário substituir | Gerar código de acesso | Emite outro código; o anterior deixa de funcionar. |
| Desinstalou/reinstalou o aplicativo | Cliente reinstalou o aplicativo | Revoga a instalação anterior e gera o novo código na mesma licença. |
| Celular novo | Trocar de celular | Revoga o aparelho anterior e gera o código para o novo. Um aparelho por licença. |
| Precisa apenas remover o vínculo anterior | Liberar outra ativação | Revoga o aparelho e as sessões. Gere o código quando o cliente estiver pronto. |
| Código entregue à pessoa errada | Cancelar código emitido | Invalida o código sem emitir outro. |
| Celular perdido, roubado ou suspensão de suporte | Bloquear acesso | Suspende novas sessões e revoga as existentes. |
| Bloqueio resolvido, pagamento confirmado | Desbloquear acesso | Restaura a licença, preservando o aparelho autorizado. |
| App precisa renovar sua sessão com a chave já salva | Reconectar aplicativo | Revoga a sessão; o mesmo aparelho pode abrir outra. |

As ações disponíveis seguem o estado atual, reconferido antes de abrir a confirmação e novamente antes da operação. O botão do cabeçalho leva à busca do cliente; abrir a página não emite código. Cada licença permite um celular: para dois aparelhos simultâneos, use duas licenças. Uma licença nova vem do cadastro comercial do Station; confira a compra em Vendas e a confirmação em Pagamentos se o cliente não aparece. Pagamento/entrega comercial pendentes não podem ser liberados pelo painel. A troca/reinstalação não cria licença ou venda. O servidor não apaga os arquivos locais de jogos/saves; reinstalar ou limpar o app por conta própria pode apagar seus dados locais.

WhatsApp é opcional e desmarcado inicialmente. Marcar a opção coloca a mensagem na fila existente; a página informa fila, sem afirmar entrega. Não há envio automático por abrir o cadastro. Os testes não enviam mensagens.

Se a liberação do aparelho concluir e a emissão falhar, a página informa o estado parcial. Atualize o cadastro e use **Gerar código de acesso**. Se a resposta se perder ou aparecer conflito, confira o histórico antes de repetir. Um pedido duplicado não gera outro código nem revoga um novo vínculo.

## Fonte e isolamento

- `site/`: página, política, JavaScript, estilos e biblioteca Station; usa autenticação/CSRF/SQLite do site existente. `station-access-link.css` estiliza apenas o atalho Station na visão geral. Não publicar fixtures de teste.
- `station-issue-admin.py`: mantém o helper loopback 5194 e seus contratos legados `/licenses`, `issue-code`30min e `issue-purchase`48h. Adiciona `/management/*` e encaminha ao backend privado.
- `TurboRamaSuiteAdminServer`: instância dedicada `turborama-station-management.service`, usuário administrativo existente, socket Unix em `/run`. `STATION_MANAGEMENT_ONLY=1` limita a instância a `/station/*`, saúde e prontidão. Não substitui a unidade Suite administrativa.
- A credencial Station é fornecida pela unidade com `LoadCredential`, sem mudar o arquivo/chave da API nem ampliar suas ACLs. A instância valida a leitura e o formato antes de abrir o socket.
- Migration **030**: recibos de emissão sem código e uma view de auditoria restrita a licenças Station, com leitura apenas para o role administrativo. Não concede leitura da tabela geral de auditoria, nem da view ao role da API.
- O código de ativação é gerado com 32 bytes aleatórios. PostgreSQL guarda apenas seu HMAC/verificador, gerações, prazo, motivo e auditoria. Não há leitura de códigos antigos. O envio opcional mantém o contrato privado de fila do site.
- Senha do operador, CSRF, motivo, gerações esperadas, sessão alvo, limite de tentativas e identificador único são conferidos antes das alterações. A geração é reconferida dentro da transação. Não há repetição automática após falha.

## Publicação e retorno

### Ajuste de acesso aos códigos — 06/10/2026

`python3 ops/station-admin/deploy-codes-panel.py` confere os hashes e a sintaxe sem publicar. Depois de testar e commitar a fonte, `--apply` atualiza quatro arquivos Station, adiciona um CSS do atalho e aplica substituições exatas em quatro templates de navegação. O manifesto é `codes-panel-20261006.json`; qualquer arquivo divergente interrompe a publicação. As cópias preservam proprietário, grupo e permissões. O backup privado contém os originais e `state.json`; uma falha restaura apenas os arquivos deste ajuste. Não altera banco, backend, serviço, senha ou licença, nem envia WhatsApp.

Retorno deste ajuste: `python3 ops/station-admin/deploy-codes-panel.py --rollback /home/lz-servidor/station-codes-panel-backup-20261006-...`. O script confere todos os hashes antes de restaurar, recusa sobrepor uma edição posterior e remove apenas o CSS novo criado por esta publicação. Use o caminho exato registrado no resultado privado.

### Publicação inicial do módulo — 03/10/2026

`deploy.py --package <diretório>` exige autenticação nativa root. O pacote contém backend publicado, cinco arquivos de site, helper, migration e manifesto de hashes ligado ao commit. Antes de alterar produção, cria backup privado e restaura o dump em PostgreSQL temporário. Os destinos precisam estar ausentes; não execute novamente para emitir códigos ou substituir estado.

Altera somente a seção Station do site, adiciona a nova unidade privada e um drop-in do helper Station. Aplica a migration aditiva030. Reinicia somente o helper5194 e inicia a administração Station. Não reinicia a API5192, PIX, Suite, ES, Nginx ou os demais serviços. Se o cache de PHP estiver configurado para não revalidar arquivos, faz recarga graciosa apenas do FPM TurboBox.

O teste de produção cria uma licença/aparelhos sintéticos, verifica os fluxos pelo helper instalado e API HTTPS e remove seus registros com marcador de propriedade. Não modifica compradores reais, não faz login como um operador real e não envia WhatsApp. Hashes públicos de CSS/JS e redirecionamento do administrador anônimo também são conferidos. O navegador autenticado é testado numa cópia privada usando os mesmos arquivos e dados sintéticos.

Retorno autorizado: autenticação nativa root e `python3 ops/station-admin/deploy.py --rollback`. Restaura apenas os arquivos Station e o helper anterior; encerra/remove a unidade/configuração própria. Preserva o schema aditivo, backup, segredo privado e release. Não restaura todo o banco sobre produção e não desfaz ações administrativas concluídas por operadores.

Se a publicação retornar após preparar o banco, `--resume <pacote>` exige backup restaurado, checksum030 idêntico, página original restaurada e serviços/conteúdo preservados. Instala uma release nova sem repetir a migration nem substituir o backup. Recusa repetir uma publicação concluída.

Backup privado: `/mnt/DADOS/station-admin-panel-backup-20261003`, modo0700; dump0600. Nunca versionar seu conteúdo. A evidência pública é sanitizada e fica no handoff técnico único do Station.

## Validação reproduzível

```bash
STATION_HTTP_PANEL_CHECKS=1 \
STATION_HTTP_API_DLL=/caminho/da/release/TurboRamaSuiteOnlineServer.dll \
STATION_HTTP_ADMIN_DLL=/caminho/da/release/TurboRamaSuiteAdminServer.dll \
pg_virtualenv python3 tests/TurboRamaSuiteOnlineServer.Tests/station_http_smoke.py
```

O runner exige `pg_virtualenv` e confere o diretório do cluster. Exercita os roles reais, os contratos de código, as ações, os bloqueios, a assinatura Android, o PHP protegido, Chrome desktop/mobile, senha incorreta, CSRF, exibição privada do código e ausência de armazenamento no navegador. Os fixtures de autenticação ficam somente na pasta temporária e nunca fazem parte do pacote.
