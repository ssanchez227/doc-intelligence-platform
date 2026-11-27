variable "web_redirect_uris" {
  description = "Where Entra may send the web app after sign-in: the local dev server and each environment's web URL, each ending in a slash."
  type        = list(string)
}
