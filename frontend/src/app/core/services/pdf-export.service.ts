import { Injectable } from '@angular/core';
import jsPDF from 'jspdf';
import autoTable from 'jspdf-autotable';

export interface PdfTableSection {
  heading?: string;
  columns: string[];
  rows: (string | number)[][];
}

export interface PdfReportSpec {
  title: string;
  /** Shown under the title, e.g. "Generated 31 Jul 2026" or a filter summary. */
  subtitle?: string;
  sections: PdfTableSection[];
}

/**
 * Generates and downloads a PDF report entirely client-side from data
 * already loaded into the app (no server round-trip / server-side PDF
 * rendering needed) — see Final_version_fix.docx item: "PDF export for
 * reports". Each report page in the app assembles its own PdfReportSpec
 * from whatever service data it already has, and hands it to export().
 */
@Injectable({ providedIn: 'root' })
export class PdfExportService {
  export(spec: PdfReportSpec, fileName: string): void {
    const doc = new jsPDF({ orientation: 'landscape' });
    const marginX = 14;
    let cursorY = 18;

    doc.setFontSize(16);
    doc.text(spec.title, marginX, cursorY);
    cursorY += 7;

    if (spec.subtitle) {
      doc.setFontSize(10);
      doc.setTextColor(100);
      doc.text(spec.subtitle, marginX, cursorY);
      doc.setTextColor(0);
      cursorY += 6;
    }

    for (const section of spec.sections) {
      if (section.heading) {
        cursorY += 4;
        doc.setFontSize(12);
        doc.text(section.heading, marginX, cursorY);
        cursorY += 2;
      }

      if (section.rows.length === 0) {
        doc.setFontSize(10);
        doc.setTextColor(120);
        doc.text('No data for this section.', marginX, cursorY + 6);
        doc.setTextColor(0);
        cursorY += 14;
        continue;
      }

      autoTable(doc, {
        startY: cursorY + 2,
        head: [section.columns],
        body: section.rows,
        styles: { fontSize: 9, cellPadding: 2.5 },
        headStyles: { fillColor: [30, 58, 95] }, // matches the app's navy accent
        margin: { left: marginX, right: marginX },
      });

      // jsPDF-autotable attaches the resulting Y position onto the doc instance.
      cursorY = (doc as unknown as { lastAutoTable: { finalY: number } }).lastAutoTable.finalY + 10;
    }

    doc.save(fileName.endsWith('.pdf') ? fileName : `${fileName}.pdf`);
  }
}
