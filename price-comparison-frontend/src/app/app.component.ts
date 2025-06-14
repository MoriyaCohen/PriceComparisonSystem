import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { XmlUploadComponent } from './components/xml-upload/xml-upload.component';
@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent {
  title = 'price-comparison-frontend';
}

