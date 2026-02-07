// ============================================================================
// Program.cs — Point d'entrée de l'application Blazor Server
// ============================================================================
// Ce fichier configure et lance l'application web ASP.NET Core en mode
// "Blazor Server". Blazor Server exécute la logique des composants côté
// serveur et communique avec le navigateur via une connexion SignalR (WebSocket).
//
// L'application suit une architecture MVVM en trois couches :
//   ViewBlazor (UI) → ViewModel → Model (Data/EF Core)
// ============================================================================

// --- Imports ----------------------------------------------------------------

// Couche Model : entités, DbContext, repositories, extensions DI
using BlazorServerApp.Model;
using BlazorServerApp.Model.Data;
using BlazorServerApp.Model.Entities;

// Couche ViewModel : extensions DI pour enregistrer les ViewModels
using BlazorServerApp.ViewModel;

// Couche ViewMCP : serveur Model Context Protocol pour l'intégration IA
using BlazorServerApp.ViewMCP;

// Radzen Blazor : bibliothèque de composants UI (remplace Bootstrap)
using Radzen;

// Authentification et claims (identité de l'utilisateur)
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

// Entity Framework Core : accès à la base de données
using Microsoft.EntityFrameworkCore;

// ============================================================================
// 1. CRÉATION DU BUILDER
// ============================================================================
// WebApplication.CreateBuilder initialise la configuration par défaut :
//   - Lecture de appsettings.json / appsettings.{Environment}.json
//   - Variables d'environnement, user-secrets (en Development)
//   - Logging (Console, Debug, EventSource, EventLog)
//   - Kestrel comme serveur web
// Le paramètre 'args' permet de passer des arguments en ligne de commande.
var builder = WebApplication.CreateBuilder(args);

// ============================================================================
// 2. ENREGISTREMENT DES SERVICES (Injection de Dépendances)
// ============================================================================
// Chaque appel Add*() enregistre des services dans le conteneur DI.
// Ces services seront ensuite injectés automatiquement là où ils sont requis.

// --- Razor Pages -----------------------------------------------------------
// Enregistre le support des Razor Pages (.cshtml).
// Utilisé ici pour les pages Login et Logout qui nécessitent un accès direct
// à HttpContext (impossible dans un composant Blazor Server).
builder.Services.AddRazorPages();

// --- Blazor Server ---------------------------------------------------------
// Enregistre le service Blazor Server (composants .razor exécutés côté serveur).
// La communication UI ↔ Serveur se fait via SignalR Hub.
builder.Services.AddServerSideBlazor()
    .AddHubOptions(options =>
    {
        // Augmente la taille max des messages SignalR à 10 Mo (défaut : 32 Ko).
        // Nécessaire pour le FileFieldViewModel qui transmet des fichiers
        // encodés en base64 via la connexion SignalR.
        options.MaximumReceiveMessageSize = 10 * 1024 * 1024; // 10 MB
    });

// --- Radzen Components -----------------------------------------------------
// Enregistre les services requis par la bibliothèque Radzen Blazor :
//   - DialogService (boîtes de dialogue modales)
//   - NotificationService (notifications toast)
//   - TooltipService (infobulles au survol)
//   - ContextMenuService (menus contextuels)
builder.Services.AddRadzenComponents();

// --- Couches ViewModel + Model ---------------------------------------------
// AddViewModels() est une méthode d'extension définie dans
// BlazorServerApp.ViewModel/DependencyInjection.cs.
// Elle enregistre :
//   1. La couche Model (via AddModel()) :
//      - IDbContextFactory<ApplicationDbContext> (Singleton, pooled)
//      - IUnitOfWorkFactory (Singleton)
//      - Les repositories génériques
//   2. La couche ViewModel :
//      - ViewModelDiscoveryService
//      - Les factories d'EntityViewModel
//
// La connection string "DefaultConnection" est lue depuis appsettings.json
// et pointe vers la base SQLite (BlazorApp.db).
// L'opérateur '!' (null-forgiving) indique qu'on est certain que la valeur
// n'est pas null (lève une exception sinon).
builder.Services.AddViewModels(builder.Configuration.GetConnectionString("DefaultConnection")!);

// --- Serveur MCP (Model Context Protocol) ----------------------------------
// AddViewMcp() enregistre les services MCP qui exposent automatiquement
// les données de l'application via des "tools" pour les assistants IA.
// Les tools sont auto-découverts à partir des RootViewModels et de leurs
// CollectionFieldViewModel<T>. Ex : PersonListViewModel.Persons → GetAllPersons
builder.Services.AddViewMcp();

// ============================================================================
// 3. AUTHENTIFICATION
// ============================================================================
// L'application utilise une double authentification :
//   1. Cookie : stocke la session utilisateur dans un cookie HTTP
//   2. OpenID Connect (OIDC) : permet la connexion via un fournisseur
//      externe (ex: Google, Microsoft Entra ID)

builder.Services.AddAuthentication(options =>
{
    // Schéma par défaut : Cookie
    // → Pour chaque requête, ASP.NET vérifie le cookie pour identifier l'user.
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;

    // Schéma de challenge par défaut : Cookie (redirige vers /Login)
    // → Si l'user n'est pas authentifié, il est redirigé vers la page de login.
    // Note : on utilise Cookie et non OIDC ici car la page Login offre le choix
    // entre connexion locale (email/password) et connexion OIDC (Google).
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})

// --- Configuration du Cookie -----------------------------------------------
.AddCookie(options =>
{
    // Chemin vers la page de login (Razor Page, pas un composant Blazor).
    // Si un utilisateur non authentifié accède à une page protégée [Authorize],
    // il sera automatiquement redirigé vers cette URL.
    options.LoginPath = "/Login";
})

// --- Configuration OpenID Connect (OIDC) -----------------------------------
// OIDC est un protocole d'authentification basé sur OAuth 2.0 qui permet
// la connexion via des fournisseurs d'identité externes (Google, etc.).
.AddOpenIdConnect(options =>
{
    // Lecture de la section "OpenIdConnect" dans la configuration.
    // Les secrets (ClientId, ClientSecret) sont stockés dans user-secrets
    // en développement (jamais dans appsettings.json pour des raisons de sécurité).
    var oidc = builder.Configuration.GetSection("OpenIdConnect");

    // URL du fournisseur d'identité (ex: https://accounts.google.com)
    // Le middleware OIDC découvre automatiquement les endpoints via
    // {Authority}/.well-known/openid-configuration
    options.Authority = oidc["Authority"];

    // Identifiant de l'application enregistrée auprès du fournisseur
    options.ClientId = oidc["ClientId"];

    // Secret partagé entre l'application et le fournisseur (confidentiel)
    options.ClientSecret = oidc["ClientSecret"];

    // "code" = Authorization Code Flow (le plus sécurisé pour les apps serveur).
    // Le navigateur reçoit un code temporaire, puis le serveur l'échange
    // directement contre des tokens (sans exposer les tokens au navigateur).
    options.ResponseType = "code";

    // Sauvegarde les tokens (access_token, id_token, refresh_token) dans
    // le cookie d'authentification pour un usage ultérieur si nécessaire.
    options.SaveTokens = true;

    // Scopes demandés au fournisseur d'identité :
    options.Scope.Add("openid");   // Obligatoire pour OIDC : retourne le sub (identifiant unique)
    options.Scope.Add("profile");  // Nom, prénom, photo de profil
    options.Scope.Add("email");    // Adresse email de l'utilisateur

    // Après l'authentification, le middleware fait un appel supplémentaire
    // au UserInfo endpoint du fournisseur pour récupérer tous les claims
    // (certains fournisseurs ne mettent pas tout dans l'id_token).
    options.GetClaimsFromUserInfoEndpoint = true;

    // URL de callback où le fournisseur OIDC redirige après l'authentification.
    // Cette URL doit être enregistrée dans la console du fournisseur
    // (ex: Google Cloud Console → Authorized redirect URIs).
    // Le middleware intercepte automatiquement les requêtes sur ce chemin.
    options.CallbackPath = "/signin-oidc";

    // --- Événements OIDC ---------------------------------------------------
    // Permet d'intercepter différentes étapes du flux d'authentification
    // pour ajouter une logique métier personnalisée.
    options.Events = new OpenIdConnectEvents
    {
        // OnTicketReceived est déclenché après que le fournisseur OIDC a validé
        // l'utilisateur et AVANT que le cookie de session soit créé.
        // C'est le moment idéal pour :
        //   1. Auto-provisionner un utilisateur local en base de données
        //   2. Enrichir les claims avec des données locales (ex: User.Id)
        OnTicketReceived = async context =>
        {
            // Récupère l'email depuis les claims retournés par le fournisseur OIDC.
            // ClaimTypes.Email = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress"
            var email = context.Principal?.FindFirstValue(ClaimTypes.Email);

            // Si aucun email n'est fourni, on ne peut pas identifier l'utilisateur
            // → on laisse le flux continuer sans modification.
            if (string.IsNullOrEmpty(email)) return;

            // --- Auto-provisioning de l'utilisateur local ---
            // On utilise IDbContextFactory (et non un DbContext injecté) car
            // cet événement s'exécute en dehors du scope DI normal d'une requête.
            // CreateDbContextAsync() crée un DbContext éphémère, libéré par 'await using'.
            var dbFactory = context.HttpContext.RequestServices
                .GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var db = await dbFactory.CreateDbContextAsync();

            // Cherche un utilisateur local dont le UserName correspond à l'email OIDC.
            var user = await db.Users.FirstOrDefaultAsync(u => u.UserName == email);

            if (user == null)
            {
                // Premier login OIDC avec cet email → création automatique
                // d'un utilisateur local. Le mot de passe est vide car
                // l'authentification se fait via le fournisseur externe.
                user = new User { UserName = email, Password = string.Empty };
                db.Users.Add(user);
                await db.SaveChangesAsync();
            }

            // --- Enrichissement des claims ---
            // On ajoute le claim NameIdentifier avec l'Id local de l'utilisateur.
            // Cela permet au reste de l'application d'identifier l'utilisateur
            // par son Id en base, indépendamment du fournisseur d'authentification.
            var identity = context.Principal!.Identity as ClaimsIdentity;
            identity?.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));

            // Si le fournisseur OIDC n'a pas retourné de claim "Name"
            // (Identity.Name serait null), on ajoute l'email comme nom d'affichage.
            // Cela garantit que l'UI peut toujours afficher un nom via
            // context.User.Identity.Name (utilisé dans MainLayout / AuthorizeView).
            if (string.IsNullOrEmpty(context.Principal.Identity?.Name))
            {
                identity?.AddClaim(new Claim(ClaimTypes.Name, email));
            }
        }
    };
});

// --- Logging ---------------------------------------------------------------
// Enregistre les services de logging (Console, Debug, etc.).
// Note : AddLogging() est déjà appelé implicitement par CreateBuilder(),
// mais cet appel explicite permet d'ajouter des configurations supplémentaires
// si nécessaire (ex: filtrage par niveau, ajout de providers).
builder.Services.AddLogging();

// ============================================================================
// 4. CONSTRUCTION DE L'APPLICATION
// ============================================================================
// Build() finalise la configuration et crée l'instance WebApplication.
// À partir d'ici, on ne peut plus enregistrer de services, seulement
// configurer le pipeline HTTP (middlewares) et les endpoints.
var app = builder.Build();

// ============================================================================
// 5. MIGRATION AUTOMATIQUE DE LA BASE DE DONNÉES
// ============================================================================
// MigrateDatabaseAsync() est une méthode d'extension (définie dans le projet Model)
// qui applique toutes les migrations EF Core en attente au démarrage.
// Cela garantit que le schéma de la base SQLite est toujours à jour,
// sans avoir à exécuter manuellement 'dotnet ef database update'.
// ⚠️ En production, on préfère généralement les migrations manuelles
// pour éviter les surprises, mais pour SQLite c'est acceptable.
await app.Services.MigrateDatabaseAsync();

// ============================================================================
// 6. PIPELINE HTTP (Middlewares)
// ============================================================================
// Les middlewares s'exécutent dans l'ordre pour chaque requête HTTP entrante.
// L'ordre est CRITIQUE : chaque middleware peut court-circuiter la chaîne.
//
// Requête HTTP → StaticFiles → Routing → Authentication → Authorization → Endpoint
//            ←                                                            ←

// --- Gestion des erreurs (Production uniquement) ---------------------------
if (!app.Environment.IsDevelopment())
{
    // En production : redirige vers /Error en cas d'exception non gérée.
    // En développement : la Developer Exception Page (activée par défaut)
    // affiche les détails de l'erreur directement dans le navigateur.
    app.UseExceptionHandler("/Error");

    // Active HSTS (HTTP Strict Transport Security) : indique au navigateur
    // de toujours utiliser HTTPS pour ce domaine pendant une durée définie.
    // Cela protège contre les attaques de type downgrade (HTTPS → HTTP).
    app.UseHsts();
}

// --- Fichiers statiques ----------------------------------------------------
// Sert les fichiers du dossier wwwroot/ (CSS, JS, images, favicon, etc.)
// sans passer par le reste du pipeline. C'est le middleware le plus rapide
// car il court-circuite la chaîne dès qu'un fichier statique est trouvé.
app.UseStaticFiles();

// --- Routage ---------------------------------------------------------------
// Active le système de routage d'ASP.NET Core.
// Ce middleware analyse l'URL de la requête et détermine quel endpoint
// doit la traiter (Razor Page, Blazor Hub, API, etc.).
// Il ne fait que SÉLECTIONNER l'endpoint ; l'exécution se fait plus tard.
app.UseRouting();

// --- Authentification ------------------------------------------------------
// Examine la requête pour identifier l'utilisateur (lecture du cookie,
// validation du token OIDC, etc.) et crée le ClaimsPrincipal
// (HttpContext.User) qui représente l'identité de l'utilisateur.
// ⚠️ DOIT être AVANT UseAuthorization().
app.UseAuthentication();

// --- Autorisation ----------------------------------------------------------
// Vérifie si l'utilisateur authentifié a le droit d'accéder à la ressource
// demandée (attributs [Authorize], politiques d'autorisation, rôles, etc.).
// Si non autorisé → redirige vers la page de login (via le ChallengeScheme).
// ⚠️ DOIT être APRÈS UseAuthentication() et AVANT les endpoints.
app.UseAuthorization();

// ============================================================================
// 7. MAPPING DES ENDPOINTS
// ============================================================================
// Chaque Map*() associe un pattern d'URL à un handler spécifique.

// --- Endpoints d'upload (désactivé) ----------------------------------------
// Endpoints REST pour l'upload de fichiers (utilisé par HtmlFieldViewModel
// pour l'insertion d'images dans l'éditeur HTML Radzen).
//app.MapUploadEndpoints(); //Endpoints for file uploads

// --- MCP Server ------------------------------------------------------------
// MapViewMcp() expose le serveur MCP à l'URL /mcp.
// Les assistants IA peuvent s'y connecter pour lire les données de
// l'application via les tools auto-découverts (ex: GetAllPersons).
app.MapViewMcp();

// --- Razor Pages -----------------------------------------------------------
// Mappe les Razor Pages (.cshtml) du dossier Pages/.
// Utilisé pour Login.cshtml et Logout.cshtml qui nécessitent
// un accès direct à HttpContext.SignInAsync/SignOutAsync.
app.MapRazorPages();

// --- Blazor SignalR Hub ----------------------------------------------------
// Mappe le hub SignalR de Blazor Server sur /_blazor (par défaut).
// Toute la communication entre les composants Blazor (côté serveur) et
// le navigateur (côté client) passe par cette connexion WebSocket.
// Chaque onglet du navigateur établit sa propre connexion SignalR.
app.MapBlazorHub();

// --- Fallback → _Host.cshtml -----------------------------------------------
// Pour toute URL qui ne correspond à aucun endpoint ci-dessus,
// sert la page _Host.cshtml. C'est le point d'entrée HTML de Blazor Server :
//   - Charge le CSS et le JS nécessaires
//   - Contient la balise <component type="typeof(App)" /> qui démarre Blazor
//   - Est protégée par [Authorize] → redirige vers /Login si non authentifié
// Le routage côté client de Blazor (Router dans App.razor) prend ensuite
// le relais pour afficher le bon composant selon l'URL.
app.MapFallbackToPage("/_Host");

// ============================================================================
// 8. DÉMARRAGE DE L'APPLICATION
// ============================================================================
// Lance le serveur web Kestrel et commence à écouter les requêtes HTTP.
// Cette méthode est bloquante : elle ne retourne que lorsque l'application
// s'arrête (via Ctrl+C, SIGTERM, ou app.StopAsync()).
app.Run();
