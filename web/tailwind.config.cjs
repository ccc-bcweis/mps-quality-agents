// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
const fs = require("fs");
const path = require("path");

const designTheme = path.resolve(__dirname, "../design/brand/tailwind.theme.js");
const fallbackTheme = path.resolve(__dirname, "brand/tailwind.theme.cjs");
const mps = require(fs.existsSync(designTheme) ? designTheme : fallbackTheme);

/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ["./index.html", "./src/**/*.{ts,tsx}"],
  theme: { extend: mps.extend },
  plugins: [],
};
