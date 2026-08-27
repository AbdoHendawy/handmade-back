/**
 * Enrich openapi-to-postman output with sample bodies, Bearer auth,
 * token capture scripts, and a ready-to-use environment.
 */
const fs = require("fs");
const path = require("path");

const root = __dirname;
const collectionPath = path.join(root, "Handmade-API.postman_collection.json");
const envPath = path.join(root, "Handmade-Local.postman_environment.json");

const SAMPLE_UUID = "11111111-1111-1111-1111-111111111111";
const SAMPLE_UUID_2 = "22222222-2222-2222-2222-222222222222";
const SAMPLE_UUID_3 = "33333333-3333-3333-3333-333333333333";

const bodyByPathHint = [
  {
    match: (url) => /\/auth\/register$/i.test(url),
    body: {
      email: "customer@example.com",
      password: "Customer1!",
      firstName: "Sara",
      lastName: "Ali",
    },
    captureTokens: true,
  },
  {
    match: (url, item) =>
      /\/auth\/login$/i.test(url) && !/Customer/i.test(item?.name || ""),
    body: {
      email: "{{adminEmail}}",
      password: "{{adminPassword}}",
    },
    captureTokens: true,
    name: "Login (Admin seed)",
  },
  {
    match: (url) => /\/auth\/google$/i.test(url),
    body: { idToken: "{{googleIdToken}}" },
    captureTokens: true,
  },
  {
    match: (url) => /\/auth\/refresh$/i.test(url),
    body: { refreshToken: "{{refreshToken}}" },
    captureTokens: true,
  },
  {
    match: (url) => /\/auth\/logout$/i.test(url),
    body: { refreshToken: "{{refreshToken}}" },
  },
  {
    match: (url) => /\/cart\/items$/i.test(url),
    body: {
      productId: "{{productId}}",
      variantId: null,
      quantity: 1,
    },
  },
  {
    match: (url) => /\/cart\/items\//i.test(url),
    method: "PUT",
    body: { quantity: 2 },
  },
  {
    match: (url) => /\/checkout$/i.test(url),
    body: {
      recipientName: "Sara Ali",
      phone: "+201001234567",
      addressLine1: "12 Nile Street",
      addressLine2: "Apt 4",
      city: "Cairo",
      governorate: "Cairo",
      postalCode: "11511",
      notes: "Call before delivery",
    },
  },
  {
    match: (url) => /\/seller\/applications$/i.test(url),
    body: {
      businessName: "Handmade Studio",
      description: "Ceramic and wood crafts",
      phone: "+201001234567",
    },
  },
  {
    match: (url) => /\/seller\/profile$/i.test(url),
    body: {
      businessName: "Handmade Studio",
      description: "Updated studio description",
      phone: "+201009998887",
    },
  },
  {
    match: (url) => /\/seller\/products$/i.test(url) && !/variants|images|stock/.test(url),
    method: "POST",
    body: {
      name: "Blue Ceramic Mug",
      description: "Hand-thrown stoneware mug",
      categoryId: "{{categoryId}}",
      price: 250,
      currency: "EGP",
      slug: "blue-ceramic-mug",
      stockQuantity: 10,
    },
  },
  {
    match: (url) => /\/seller\/products\/[^/]+$/i.test(url),
    method: "PUT",
    body: {
      name: "Blue Ceramic Mug (Updated)",
      description: "Updated description",
      categoryId: "{{categoryId}}",
      price: 275,
      currency: "EGP",
      slug: "blue-ceramic-mug",
      stockQuantity: 8,
    },
  },
  {
    match: (url) => /\/images$/i.test(url) && !/upload|reorder/.test(url),
    method: "POST",
    body: {
      storageKey: "products/sample/mug-1.jpg",
      url: "http://localhost:9000/handmade/products/sample/mug-1.jpg",
      sortOrder: 0,
      isPrimary: true,
    },
  },
  {
    match: (url) => /\/images\/reorder$/i.test(url),
    body: { imageIds: ["{{imageId}}", "{{imageId2}}"] },
  },
  {
    match: (url) => /\/variants$/i.test(url),
    method: "POST",
    body: {
      name: "Large",
      sku: "MUG-BLU-L",
      price: 280,
      currency: "EGP",
      stockQuantity: 5,
    },
  },
  {
    match: (url) => /\/variants\/[^/]+$/i.test(url),
    method: "PUT",
    body: {
      name: "Large",
      sku: "MUG-BLU-L",
      price: 290,
      currency: "EGP",
      stockQuantity: 4,
    },
  },
  {
    match: (url) => /\/stock$/i.test(url),
    body: { stockQuantity: 20 },
  },
  {
    match: (url) => /\/admin\/categories$/i.test(url),
    method: "POST",
    body: {
      name: "Ceramics",
      slug: "ceramics",
      description: "Handmade ceramic pieces",
      parentCategoryId: null,
    },
  },
  {
    match: (url) => /\/admin\/categories\/[^/]+$/i.test(url),
    method: "PUT",
    body: {
      name: "Ceramics",
      slug: "ceramics",
      description: "Updated ceramics category",
      parentCategoryId: null,
    },
  },
  {
    match: (url) => /\/admin\/products\/[^/]+\/reject$/i.test(url),
    body: { reason: "Missing product photos quality" },
  },
  {
    match: (url) => /\/admin\/seller-applications\/[^/]+\/reject$/i.test(url),
    body: { reason: "Incomplete business information" },
  },
  {
    match: (url) => /\/admin\/sellers\/[^/]+\/suspend$/i.test(url),
    body: { reason: "Policy violation" },
  },
  {
    match: (url) => /\/admin\/notifications$/i.test(url),
    method: "POST",
    body: {
      type: "System",
      title: "Welcome",
      body: "Thanks for joining Handmade",
      userId: "{{userId}}",
      roleName: null,
      dataJson: null,
      idempotencyKey: "welcome-{{$guid}}",
    },
  },
  {
    match: (url) => /\/admin\/notifications\/[^/]+$/i.test(url),
    method: "PUT",
    body: {
      title: "Updated title",
      body: "Updated body",
      isRead: false,
      dataJson: null,
    },
  },
];

const captureScript = [
  "if (pm.response.code >= 200 && pm.response.code < 300) {",
  "  try {",
  "    const data = pm.response.json();",
  "    if (data.accessToken) pm.collectionVariables.set('accessToken', data.accessToken);",
  "    if (data.refreshToken) pm.collectionVariables.set('refreshToken', data.refreshToken);",
  "    if (data.user && data.user.id) pm.collectionVariables.set('userId', data.user.id);",
  "    if (data.id && !data.accessToken) pm.collectionVariables.set('lastId', data.id);",
  "  } catch (e) {}",
  "}",
].join("\n");

function requestUrlPath(req) {
  if (!req || !req.url) return "";
  const parts = Array.isArray(req.url.path) ? req.url.path : [];
  return "/" + parts.join("/");
}

function ensureEvent(item, listen, execLines) {
  item.event = item.event || [];
  let ev = item.event.find((e) => e.listen === listen);
  if (!ev) {
    ev = { listen, script: { type: "text/javascript", exec: [] } };
    item.event.push(ev);
  }
  ev.script.exec = execLines;
}

function walk(items) {
  for (const item of items) {
    if (item.item) {
      walk(item.item);
      continue;
    }
    const req = item.request;
    if (!req) continue;

    // Prefer Bearer collection auth; drop per-request noauth noise
    if (req.auth && req.auth.type === "noauth") {
      delete req.auth;
    }

    // Auth endpoints should stay public
    const urlPath = requestUrlPath(req);
    if (/\/auth\/(register|login|google|refresh)$/i.test(urlPath)) {
      req.auth = { type: "noauth" };
    }

    // Fix Accept header for JSON APIs
    if (Array.isArray(req.header)) {
      for (const h of req.header) {
        if (h.key === "Accept" && String(h.value).includes("text/plain")) {
          h.value = "application/json";
        }
      }
    }

    // Path variables -> Postman variables
    if (req.url && Array.isArray(req.url.variable)) {
      for (const v of req.url.variable) {
        const key = v.key;
        if (key === "id" || key === "productId") v.value = "{{productId}}";
        else if (key === "orderId") v.value = "{{orderId}}";
        else if (key === "orderGroupId") v.value = "{{orderGroupId}}";
        else if (key === "userId") v.value = "{{userId}}";
        else if (key === "imageId") v.value = "{{imageId}}";
        else if (key === "variantId") v.value = "{{variantId}}";
        else if (key === "slug") v.value = "{{productSlug}}";
        else if (!v.value || v.value === "" || v.value === "<string>") {
          v.value = `{{${key}}}`;
        }
      }
    }

    if (req.body && req.body.mode === "raw") {
      const hint = bodyByPathHint.find((h) => {
      if (!h.match(urlPath, item)) return false;
      if (h.method && String(req.method).toUpperCase() !== h.method) return false;
      return true;
    });
      if (hint) {
        req.body.raw = JSON.stringify(hint.body, null, 2);
        if (hint.name) item.name = hint.name;
        if (hint.captureTokens) {
          ensureEvent(item, "test", captureScript.split("\n"));
        }
      } else if (typeof req.body.raw === "string") {
        // Replace generic <string>/<uuid> placeholders with usable samples
        req.body.raw = req.body.raw
          .replace(/"<string>"/g, '"sample"')
          .replace(/"<uuid>"/g, `"${SAMPLE_UUID}"`)
          .replace(/: "<uuid>"/g, `: "${SAMPLE_UUID}"`);
      }
    }

    // Multipart upload: leave mode, set helpful description
    if (req.body && req.body.mode === "formdata") {
      item.description =
        (item.description || "") +
        "\nSelect a real image file for the `file` field before sending.";
    }
  }
}

const collection = JSON.parse(fs.readFileSync(collectionPath, "utf8"));

collection.info.name = "Handmade API";
collection.info.description =
  "Generated from /openapi/v1.json with sample bodies for local try-outs.\n\n" +
  "1) Import this collection + Handmade-Local environment\n" +
  "2) Select environment Handmade Local\n" +
  "3) Run Auth → Login (Admin seed) to fill accessToken automatically\n" +
  "4) Replace {{productId}} / {{categoryId}} etc. from responses as you go";

collection.auth = {
  type: "bearer",
  bearer: [{ key: "token", value: "{{accessToken}}", type: "string" }],
};

collection.variable = [
  { key: "baseUrl", value: "http://localhost:5159" },
  { key: "accessToken", value: "" },
  { key: "refreshToken", value: "" },
  { key: "adminEmail", value: "admin@localhost.local" },
  { key: "adminPassword", value: "DevOnly_Admin1!" },
  { key: "userId", value: SAMPLE_UUID },
  { key: "productId", value: SAMPLE_UUID },
  { key: "categoryId", value: SAMPLE_UUID_2 },
  { key: "orderId", value: SAMPLE_UUID },
  { key: "orderGroupId", value: SAMPLE_UUID },
  { key: "imageId", value: SAMPLE_UUID },
  { key: "imageId2", value: SAMPLE_UUID_3 },
  { key: "variantId", value: SAMPLE_UUID_2 },
  { key: "productSlug", value: "blue-ceramic-mug" },
  { key: "googleIdToken", value: "PASTE_GOOGLE_ID_TOKEN" },
];

if (collection.item) walk(collection.item);

// Add a dedicated customer login request next to admin login for convenience
function findFolder(items, name) {
  for (const item of items) {
    if (item.name === name && item.item) return item;
    if (item.item) {
      const found = findFolder(item.item, name);
      if (found) return found;
    }
  }
  return null;
}

const authFolder = findFolder(collection.item, "auth");
if (authFolder && !authFolder.item.some((i) => /Customer sample/i.test(i.name || ""))) {
  const customerLogin = {
    name: "Login (Customer sample)",
    event: [{ listen: "test", script: { type: "text/javascript", exec: captureScript.split("\n") } }],
    request: {
      auth: { type: "noauth" },
      method: "POST",
      header: [
        { key: "Content-Type", value: "application/json" },
        { key: "Accept", value: "application/json" },
      ],
      body: {
        mode: "raw",
        raw: JSON.stringify(
          {
            email: "customer@example.com",
            password: "Customer1!",
          },
          null,
          2
        ),
        options: { raw: { language: "json" } },
      },
      url: {
        raw: "{{baseUrl}}/api/v1/auth/login",
        host: ["{{baseUrl}}"],
        path: ["api", "v1", "auth", "login"],
      },
      description: "Login as a registered customer (run Register first if needed).",
    },
  };
  authFolder.item.splice(2, 0, customerLogin);
}

fs.writeFileSync(collectionPath, JSON.stringify(collection, null, 2));

const environment = {
  id: "handmade-local-env",
  name: "Handmade Local",
  values: [
    { key: "baseUrl", value: "http://localhost:5159", enabled: true },
    { key: "baseUrlHttps", value: "https://localhost:7152", enabled: true },
    { key: "adminEmail", value: "admin@localhost.local", enabled: true },
    { key: "adminPassword", value: "DevOnly_Admin1!", enabled: true },
    { key: "accessToken", value: "", enabled: true },
    { key: "refreshToken", value: "", enabled: true },
    { key: "userId", value: "", enabled: true },
    { key: "productId", value: "", enabled: true },
    { key: "categoryId", value: "", enabled: true },
    { key: "orderId", value: "", enabled: true },
    { key: "orderGroupId", value: "", enabled: true },
    { key: "imageId", value: "", enabled: true },
    { key: "variantId", value: "", enabled: true },
    { key: "productSlug", value: "blue-ceramic-mug", enabled: true },
    { key: "googleIdToken", value: "", enabled: true },
  ],
  _postman_variable_scope: "environment",
};

fs.writeFileSync(envPath, JSON.stringify(environment, null, 2));

// Keep a copy of OpenAPI next to the collection
const openApiSrc = path.join(root, "..", "openapi-v1.json");
const openApiDst = path.join(root, "openapi-v1.json");
if (fs.existsSync(openApiSrc)) {
  fs.copyFileSync(openApiSrc, openApiDst);
}

console.log("Wrote", collectionPath);
console.log("Wrote", envPath);
console.log("Wrote", openApiDst);
