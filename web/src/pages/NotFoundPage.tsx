// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { btn } from "../components/ui";
import { Link } from "react-router-dom";
import { useSetLifecycle } from "../context/ConsoleContext";

export function NotFoundPage() {
  useSetLifecycle("alert");
  return (
    <div>
      <h1 className="text-mps-display font-semibold">Screen not found</h1>
      <p className="mt-2">
        <Link className={btn} to="/">Return to the production line</Link>
      </p>
    </div>
  );
}
