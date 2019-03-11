# Tutorial: Zabezpečení API v ASP.NET Core pomocí Azure AD (1. díl)

V tomto seriálu si ukážeme, jak pomocí Azure AD zabezpečit ASP.NET Core API. Začneme s jednoduchým řešením, které se bude v dalších dílech postupně rozšiřovat. V prvním díle vytvoříme API pro správu zaměstnanců a zabezpečíme jej pomocí Azure AD tak, že k němu budou moct přistupovat pouze uživatelé našeho tenantu.

Všechny zdrojové kódy [jsou dostupné na GitHubu](https://github.com/luboshl/tutorial-protect-aspnet-core-api-with-aad/tree/master/part-1/src).

## Krok 1 - vytvoření API

Vytvoříme projekt typu **ASP.NET Web Application**.

![Create Organization.API project](https://raw.githubusercontent.com/luboshl/tutorial-protect-aspnet-core-api-with-aad/master/part-1/images/01_create_organization.api_project.png)

## Krok 2 - úprava controlleru

Vygenerovaný controller upravíme tak, aby pracoval s daty o zaměstnancích.

*Pozn.: Příklad je velice zjednodušený, neřeší ani perzistenci dat v databázi.*

```csharp
[Route("api/[controller]")]
[ApiController]
public class EmployeesController : ControllerBase
{
    private static readonly Dictionary<Guid, Employee> Employees;

    static EmployeesController()
    {
        var employees = new[]
        {
            new Employee { Id = new Guid("86092B43-FFC5-4947-A850-AE890649606D"), FirstName = "John", LastName = "Doe" },
            new Employee { Id = new Guid("0C742DB0-EF36-416A-8364-69C4142DAD12"), FirstName = "Emily", LastName = "Smith" }
        };
        Employees = new Dictionary<Guid, Employee>(employees.ToDictionary(e => e.Id));
    }

    [HttpGet]
    public ActionResult<IEnumerable<Employee>> Get()
    {
        return Employees.Values.ToList();
    }

    [HttpGet("{id}")]
    public ActionResult<Employee> GetById(Guid id)
    {
        if (!Employees.TryGetValue(id, out var result))
        {
            return NotFound();
        }

        return result;
    }

    [HttpPost]
    public ActionResult Post([FromBody] Employee item)
    {
        if (item.Id == Guid.Empty)
        {
            item.Id = Guid.NewGuid();
        }

        if (!Employees.TryAdd(item.Id, item))
        {
            return BadRequest(new { Error = $"Object with Id {item.Id} already exists." });
        }

        return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
    }

    [HttpPut("{id}")]
    public ActionResult Put(Guid id, [FromBody] Employee item)
    {
        if (id != item.Id)
        {
            return BadRequest();
        }

        if (!Employees.TryGetValue(id, out _))
        {
            return NotFound();
        }

        Employees[id] = item;
        return NoContent();
    }

    [HttpDelete("{id}")]
    public ActionResult Delete(Guid id)
    {
        if (!Employees.Remove(id))
        {
            return NotFound();
        }

        return NoContent();
    }
}
```

Model třídy `Employee` je velice jednoduchý:

```csharp
public class Employee
{
    public Guid Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
}
```

V konfiguračním souboru `Properties/launchSettings.json` upravíme parametr `launchUrl` na hodnotu `"api/employees"`, pokud chceme po startu načíst adresu v prohlížeči, a  dále nastavíme port na hodnotu 3001.

Nyní můžeme projekt spustit:

![API started in console](https://raw.githubusercontent.com/luboshl/tutorial-protect-aspnet-core-api-with-aad/master/part-1/images/03_api_started_in_console.png)

Pro ověření funkčnosti použijeme aplikaci [Postman](https://www.getpostman.com). Kolekci se všemi requesty si můžete stáhnout a importovat z <https://raw.githubusercontent.com/luboshl/tutorial-protect-aspnet-core-api-with-aad/master/part-1/artefacts/Unathorized.postman_collection.json>.

![Postman - Unauthorized](https://raw.githubusercontent.com/luboshl/tutorial-protect-aspnet-core-api-with-aad/master/part-1/images/02_postman_unathorized.png)

API je funkční, umí vracet, vytvářet, upravovat a mazat záznamy, pojďme ho zabezpečit.

## Krok 3 - registrace API v Azure AD

V Azure portálu zvolíme "Azure Active Directory" -> "Add registrations (Preview)" -> "New registration":

![Azure - add API](https://raw.githubusercontent.com/luboshl/tutorial-protect-aspnet-core-api-with-aad/master/part-1/images/04_azure_add_api.png)

Nastavíme jméno pro API a zvolíme "Accounts in this organizational directory only (*&lt;název directory&gt;*)", čímž bude přístup omezen pouze na náš tenant. "Redirect URI" nevyplňujeme. Stiskneme tlačítko "Register".

![Azure - register API](https://raw.githubusercontent.com/luboshl/tutorial-protect-aspnet-core-api-with-aad/master/part-1/images/05_azure_register_api.png)

Po registraci aplikace se zobrazí přehled na záložce "Overview". Poznamenáme si hodnotu "Application (client) ID" a "Directory (tenant) ID), později je budeme potřebovat.

![Copy application ID](https://raw.githubusercontent.com/luboshl/tutorial-protect-aspnet-core-api-with-aad/master/part-1/images/06_azure_copy_application_id.png)

V nastavení registrované aplikace vystavíme API. To se provede na záložce "Expose API" a pak volbou "Add a scope".

![Add scope to API](https://raw.githubusercontent.com/luboshl/tutorial-protect-aspnet-core-api-with-aad/master/part-1/images/07_azure_add_scope_to_api.png)

Nejdříve musíme nastavit URI aplikace - potvrdíme nabídnutou hodnotu skládající se z prefixu "api://" a ID aplikace.

![Confirm application ID URI](https://raw.githubusercontent.com/luboshl/tutorial-protect-aspnet-core-api-with-aad/master/part-1/images/08_confirm_application_id_uri.png)

Vytvoříme scope s názvem "user_impersonation"
a vyplníme název pro zobrazení a popis v dialogu pro odsouhlasení přístupu aplikace k našemu API.

![Add user_impersonation scope](https://raw.githubusercontent.com/luboshl/tutorial-protect-aspnet-core-api-with-aad/master/part-1/images/09_add_user_impersonation_scope.png)

Vytvořený scope se zobrazí v seznamu.

![List of scopes](https://raw.githubusercontent.com/luboshl/tutorial-protect-aspnet-core-api-with-aad/master/part-1/images/10_scopes_list.png)

## Krok 4 - zabezpečení API

API zabezpečíme tak, aby ke controlleru pro správu zaměstnanců mohli přistupovat jen uživatelé z naší domény, kteří se ověřili vůči Azure AD.

Nejprve v souboru `Startup.cs` doplníme konfiguraci služeb o službu pro autentizaci, a nakonfigurujeme JWT Bearer. Do proměnné `tenantId` nastavíme ID našeho tenantu, do `appId` pak ID aplikace - obě hodnoty jsme si poznamenali v předchozím kroku.

Dále ve stejném souboru přidáme middleware pro autentizaci pomocí `app.UseAuthentication()`. Pozor, musí být před middlewarem pro MVC, tedy `app.UseMvc()`.

Výsledné metody `ConfigureServices` a `Configure` vypadají takto:

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2);

    var tenantId = "98245c15-c348-45a7-8be1-25afcc783931";
    var appId = "725859c1-e28a-4af0-be28-ecfd522bebd3";

    services.AddAuthentication("Bearer")
        .AddJwtBearer("Bearer", options =>
        {
            options.Authority = $"https://sts.windows.net/{tenantId}/";
            options.Audience = $"api://{appId}";
            options.RequireHttpsMetadata = true;
        });
}

public void Configure(IApplicationBuilder app, IHostingEnvironment env)
{
    if (env.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }
    else
    {
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseAuthentication();
    app.UseMvc();
}
```

Aby byl `EmployeesController` zabezpečený, přidáme třídě atribut `Authorize` z namespace `Microsoft.AspNetCore.Authorization`. To zajistí, že při přístupu k tomuto controlleru musejí všechny požadavky dle konfigurace výše obsahovat v hlavičce validní bearer token.

Pokud nyní API pustíme a pošleme do něj požadavek přes prohlížeč nebo Postmana, dostaneme chybu s HTTP kódem **401 - Unauthorized**. To je správně, v požadavku jsme žádný token nepředali, API tak požadavek odmítlo.

## Krok 5 - získání a použití access tokenu

Pro přístup k API je vyžadován token, který obsahuje potřebné informace pro ověření oprávnění k přístupu ke zdrojům vystavovaným přes API. Pro tyto účely slouží tzv. access token, který představuje oprávnění uživatele delegované na držitele tohoto tokenu. O token zažádá aplikace (= client), jejímž prostřednictvím uživatel přistupuje k API.

Aplikaci, která bude žádat o token, musíme nejprve zaregistrovat v Azure AD podobně, jako jsme regisrtovali API. V Azure portálu zvolíme "Azure Active Directory" -> "Add registrations" (Preview) -> "New registration". Nastavíme jméno pro debug klienta a opět zvolíme "Accounts in this organizational directory only (*&lt;název directory&gt;*)". Narozdíl od API vyplníme "Redirect URI" - zadáme "<https://oidcdebugger.com/debug"> (pro získání tokenu budeme používat OpenID Connect Debugger, viz dále).

![Register debug client with redirect URI](https://raw.githubusercontent.com/luboshl/tutorial-protect-aspnet-core-api-with-aad/master/part-1/images/11_register_debug_client_with_redirect_uri.png)

Opět si poznamenáme  hodnotu "Application (client) ID".

Po vytvoření registrace klienta musíme ještě v záložce "Authentication" povolit **implicit grant flow** zaškrtnutím položky "Access tokens". Důvodem je použití takového klienta pro debugování, který nemá backend jako ASP.NET MVC, PHP apod. V takových případech (typicky Javascriptové frontendy) se používá tzv. "implicit grant flow". Popis OAuth resp. OIDC flows je však mimo rámec tohoto seriálu.

![Implicit grant flow](https://raw.githubusercontent.com/luboshl/tutorial-protect-aspnet-core-api-with-aad/master/part-1/images/12_implicit_grant.png).

Tím je registrace klienta hotová, můžeme se vrhnout na získání tokenu. Pro tyto účely použijeme online aplikaci **OpenID Connect Debugger** na adrese <https://oidcdebugger.com.> Ta umí ze zadaných parametrů vytvářet OIDC požadavky a umožňuje debugovat odpovědi.

Zadáme následující parametry:

- **Authorize URI:** "<https://login.microsoftonline.com/TENANT_ID/oauth2/v2.0/authorize>" (dosadíme ID našeho tenantu)
- **Redirect URI:** "<https://oidcdebugger.com/debug"> (výchozí hodnota)
- **Client ID:** hodnota "Application (client) ID", kterou jsme si poznamenali v kroku 5 - ID debug klienta, nikoliv API
- **Scope:** "openid api://*&lt;API app ID&gt;*/user_impersonation" (dosadíme ID registrované aplikace pro API z kroku 3)
- **State:** prázdné
- **Nonce:** můžeme nechat vygenerovanou hodnotu
- **Response type:** "token"
- **Response mode:** "form_post"

![OIDC debugger](https://raw.githubusercontent.com/luboshl/tutorial-protect-aspnet-core-api-with-aad/master/part-1/images/13_oidc_debugger.png)

Po odeslání requestu tlačítkem "Send request" jsme přesměrováni na <https://login.microsoftonline.com,> kde zadáme (nebo vybereme) uživatele, pod kterým se chceme v aplikaci přihlásit. Následuje zobrazení dialogu, ve kterém dává přihlášený uživatel souhlas aplikaci (definované pomocí Client ID) k přístupu ke zdrojům (Scope) definovaným v požadavku. Scope "openid" přidává identitu uživatele, v dialogu zobrazený popisem "Sign in as you". Námi definovaný scope "api://*&lt;API app ID&gt;*/user_impersonation" pak umožňuje přístup k našemu API a v dialogu zobrazuje popis, který jsme zadali při vytváření scopu.

V případě, že je přihlášený uživatel, který má v Azure AD roli "Global administrator", je v dialogu se souhlasem ještě možnost zaškrtnout volbu "Consent on behalf of your organization". Tímto způsobem pak dostane aplikace povolení k definovaným scopům pro všechny uživatele a ti už tento dialog neuvidí a nemusejí oprávnění potvrzovat.

![Consent dialog](https://raw.githubusercontent.com/luboshl/tutorial-protect-aspnet-core-api-with-aad/master/part-1/images/14_consent.png)

Po akceptování dialogu s oprávněními dostaneme požadovaný access token:

![Received token](https://raw.githubusercontent.com/luboshl/tutorial-protect-aspnet-core-api-with-aad/master/part-1/images/15_received_token.png)

Obsah tokenu si můžeme v čitelné podobě prohlédnout např. na <https://jwt.io> nebo <http://jwt.ms>. Po zkopírování a vložení do pole "Encoded" vidíme obsah ve formě JSONu.

*Pozn.: data v JSONu jsou zkrácena.*

```json
{
  "aud": "api://725859c1-e28a-4af0-be28-ecfd522bebd3",
  "iss": "https://sts.windows.net/98245c15-c348-45a7-8be1-25afcc783931/",
  "iat": 1552142303,
  "nbf": 1552142303,
  "exp": 1552146203,
  "acr": "1",
  "appid": "fbe825db-47fe-4b57-bd7f-b5ca1ac121c8",
  "appidacr": "0",
  "family_name": "Hladík",
  "given_name": "Luboš",
  "name": "Luboš Hladík",
  "scp": "user_impersonation",
  "sub": "*****",
  "tid": "98245c15-c348-45a7-8be1-25afcc783931",
  "unique_name": "lubos.hladik@riganti.cz",
  "upn": "lubos.hladik@riganti.cz",
  "ver": "1.0"
}
```

Podstatné jsou hodnoty v těchto claimech:

- **aud**: (audience) určuje, pro koho je token určen, kdo je jeho příjemcem - v našem případě API identifikované svým ID
- **iss**: (issuer) vydavatel, tedy autorizační služba, která token vydala
- **iat, nbf, exp**: vydání a časové omezení platnosti tokenu
- **scp**: scopy, pro které (může jich být víc) je token vydaný - odpovídá oprávněním, které uživatel (případně administrátorem) potvrdil v dialogu pro odsouhlasení oprávnění pro aplikaci. Může obsahovat i scopy, které nebyly zadány v požadavku na token, ale přidají se implicitně na základě nastavení registrované aplikace (popíšeme si v některém z následujících dílů).

Token (v původní podobě zakódované pomocí Base64) nyní můžeme přidat do požadavku na API. V Postmanu. Na záložce "Authorization" zvolíme typ "Bearer Token" a vložíme token.

![Bearer in Postman](https://raw.githubusercontent.com/luboshl/tutorial-protect-aspnet-core-api-with-aad/master/part-1/images/16_postman_with_bearer_token.png)

Odešleme požadavek tlačítkem "Send" a... voilà, místo původního **401 Unauthorized** dostáváme jako odpověď HTTP kód **200 OK**.

V tuto chvíli na straně API neprobíhá kontrola předaného scopu, to bude předmětem následujícího dílu. ASP.NET Core ale za nás provede validaci předaného tokenu. Ta představuje zejména:

- kontrola **přítomnosti bearer tokenu** v hlavičce HTTP requestu
- ověření **formátu tokenu**
- ověření, že **autorita**, která token vydala (claim **iss**) odpovídá autoritě, kterou jsme konfigurovali parametrem `Authority` ve třídě `Startup`.
- ověření **podpisu tokenu** - token je podepsaný privátním klíčem serveru, který token vydal (autority). Služba si z něj stáhne veřejný klíč, kterým pak podpis ověří.
- ověření, že **audience** v tokenu (claim **aud**) odpovídá identifikaci API, jak jsme ji nastavili parametrem `Audience` ve třídě `Startup`.

Probíhají i další validace, některé je možné konfigurovat a přizpůsobit si tak celý proces validace tokenu. Důležité je, že jakákoliv chyba při validaci tokenu má za následek odmítnutí celého requestu a ten se tak vůbec nedostane ke zpracování do controlleru.

Abychom zjistili, jaké informace má controller v API k dispozici, vytvoříme si controller s názvem `AuthController`.

```csharp
[Route("api/[controller]")]
[ApiController]
[Authorize]
public class AuthController : ControllerBase
{
    [Route("info")]
    [HttpGet]
    public IActionResult Info()
    {
        return Ok(new
        {
            IsUserAuthenticated = User.Identity.IsAuthenticated,
            UserName = User.Identity.Name,
            Claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList()
        });
    }
}
```

Do Postmana přidáme nový GET request na adresu <https://localhost:3001/api/auth/info>, nastavíme bearer token a odešleme dotaz. Výstupem je informace, jestli je uživatel autentizovaný, jaké je jeho jméno a seznam claimů.

![Postman auth info](https://raw.githubusercontent.com/luboshl/tutorial-protect-aspnet-core-api-with-aad/master/part-1/images/17_postman_auth_info.png)

Naše API je zabezpečené, přihlásit se mohou jen uživatelé autentizovaní přes Azure AD v našem tenantu, a to až po schválení přístupu pro aplikaci, která se k API chce připojit.

V příštím díle si ukážeme, jak lze pomocí scopů řídit přístup k API pro aplikace a jak oprávnění pro uživatele pomocí rolí.